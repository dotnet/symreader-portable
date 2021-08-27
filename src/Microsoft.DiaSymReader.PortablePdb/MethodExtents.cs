// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;

namespace Microsoft.DiaSymReader.PortablePdb
{
    internal sealed class MethodExtents
    {
        // Consider: use array since most documents contain some methods
        // Immutable in between EnC updates.
        // { document -> extents for each method contained in the document, ordered by method id }
        private readonly Dictionary<DocumentId, ImmutableArray<MethodLineExtent>> _extentsByDocument;

        // Method extents partitioned into non-overlapping subsequences, each sorted by min line.
        // Used as cache, lock on access.
        private Dictionary<DocumentId, ImmutableArray<ImmutableArray<MethodLineExtent>>> _lazyPartitionedExtentsByDocument = new();

        public MethodExtents(PortablePdbReader pdbReader)
        {
            _extentsByDocument = GroupExtentsByDocument(GetMethodExtents(pdbReader));
        }

        internal void Update(PortablePdbReader pdbReader, MethodDebugInformationHandle methodHandle, ImmutableArray<int> deltas, int expectedSequencePointCount)
        {
            var newExtentsByDocument = GroupExtentsByDocument(GetMethodExtents(pdbReader, methodHandle, deltas, expectedSequencePointCount));

            foreach (var (documentId, newExtents) in newExtentsByDocument)
            {
                // we only created extents for a single methods and all extents of a single method within document were merged into one:
                _extentsByDocument[documentId] = UpdateExtent(_extentsByDocument[documentId], newExtents.Single());
            }
        }

        private ImmutableArray<MethodLineExtent> UpdateExtent(ImmutableArray<MethodLineExtent> extents, MethodLineExtent newExtent)
        {
            int index = extents.BinarySearch(newExtent, (x, y) => x.Method.CompareTo(y.Method));
            Debug.Assert(index >= 0);
            return extents.SetItem(index, newExtent);
        }

        internal void Update(PortablePdbReader pdbReader, Dictionary<DocumentId, List<(MethodId, int)>> lineDeltasByDocument)
        {
            foreach (var (documentId, deltas) in lineDeltasByDocument)
            {
                _extentsByDocument[documentId] = ApplyDeltas(_extentsByDocument[documentId], deltas);
            }

            var newExtentsByDocument = GroupExtentsByDocument(GetMethodExtents(pdbReader));

            foreach (var (documentId, newExtents) in newExtentsByDocument)
            {
                if (_extentsByDocument.TryGetValue(documentId, out var existingExtents))
                {
                    _extentsByDocument[documentId] = MergeExtents(existingExtents, newExtents);
                }
                else
                {
                    _extentsByDocument[documentId] = newExtents;
                }
            }

            // partitioned extents will need to be recalculated for all affected documents:
            lock (_lazyPartitionedExtentsByDocument)
            {
                foreach (var document in lineDeltasByDocument.Keys)
                {
                    _lazyPartitionedExtentsByDocument.Remove(document);
                }

                foreach (var document in newExtentsByDocument.Keys)
                {
                    _lazyPartitionedExtentsByDocument.Remove(document);
                }
            }
        }

        private static ImmutableArray<MethodLineExtent> ApplyDeltas(ImmutableArray<MethodLineExtent> extents, List<(MethodId Method, int Delta)> deltas)
        {
            // Note: extents and deltas are both ordered by method id.

            var builder = ImmutableArray.CreateBuilder<MethodLineExtent>(extents.Length);

            int i = 0, j = 0;
            while (i < extents.Length && j < deltas.Count)
            {
                if (extents[i].Method == deltas[j].Method)
                {
                    builder.Add(extents[i].ApplyDelta(deltas[j].Delta));
                    i++;
                    j++;
                }
                else if (extents[i].Method < deltas[j].Method)
                {
                    builder.Add(extents[i]);
                    i++;
                }
                else
                {
                    j++;
                }
            }

            builder.AddSubRange(extents, i);

            return builder.MoveToImmutable();
        }

        private ImmutableArray<MethodLineExtent> MergeExtents(
            ImmutableArray<MethodLineExtent> existingExtents,
            ImmutableArray<MethodLineExtent> newExtents)
        {
            // Note: extents are ordered by method id.

            var builder = ImmutableArray.CreateBuilder<MethodLineExtent>();

            int i = 0, j = 0;
            while (i < existingExtents.Length && j < newExtents.Length)
            {
                if (existingExtents[i].Method == newExtents[j].Method)
                {
                    // new extent replaces the existing one:
                    builder.Add(newExtents[j]);
                    i++;
                    j++;
                }
                else if (existingExtents[i].Method < newExtents[j].Method)
                {
                    builder.Add(existingExtents[i]);
                    i++;
                }
                else
                {
                    builder.Add(newExtents[j]);
                    j++;
                }
            }

            builder.AddSubRange(existingExtents, i);
            builder.AddSubRange(newExtents, j);

            return builder.ToImmutable();
        }

        private static Dictionary<DocumentId, ImmutableArray<MethodLineExtent>> GroupExtentsByDocument(
            IEnumerable<(DocumentId DocumentId, MethodLineExtent Extent)> methodExtents)
        {
            var builder = new Dictionary<DocumentId, ImmutableArray<MethodLineExtent>.Builder>();

            foreach (var (documentId, extent) in methodExtents)
            {
                if (!builder.TryGetValue(documentId, out var extentsInDocument))
                {
                    builder[documentId] = extentsInDocument = ImmutableArray.CreateBuilder<MethodLineExtent>();
                }

                extentsInDocument.Add(extent);
            }

            var result = new Dictionary<DocumentId, ImmutableArray<MethodLineExtent>>(builder.Count);

            foreach (var (documentId, extents) in builder)
            {
                Debug.Assert(extents.Count > 0);

                // sort by method handle:
                extents.Sort(MethodLineExtent.MethodComparer.Instance);

                // merge spans belonging to a single method:
                int j = 0;
                for (int i = 1; i < extents.Count; i++)
                {
                    if (extents[i].Method == extents[j].Method)
                    {
                        extents[j] = MethodLineExtent.Merge(extents[i], extents[j]);
                    }
                    else
                    {
                        j++;

                        if (j < i)
                        {
                            extents[j] = extents[i];
                        }
                    }
                }

                Debug.Assert(j < extents.Count);
                extents.Count = j + 1;

                result.Add(documentId, extents.ToImmutable());
            }

            return result;
        }

        private bool TryGetPartitionedExtents(DocumentId documentId, out ImmutableArray<ImmutableArray<MethodLineExtent>> partitionedExtents)
        {
            lock (_lazyPartitionedExtentsByDocument)
            {
                if (_lazyPartitionedExtentsByDocument.TryGetValue(documentId, out partitionedExtents) == true)
                {
                    return true;
                }
            }

            if (!_extentsByDocument.TryGetValue(documentId, out var extentsInDocument))
            {
                partitionedExtents = default;
                return false;
            }

            partitionedExtents = PartitionToNonOverlappingSubsequences(extentsInDocument);

            lock (_lazyPartitionedExtentsByDocument)
            {
                // Overwrite value that another thread might have calculated since we checked above.
                // The results have to be the same.
                _lazyPartitionedExtentsByDocument[documentId] = partitionedExtents;
            }

            return true;
        }

        private static ImmutableArray<ImmutableArray<MethodLineExtent>> PartitionToNonOverlappingSubsequences(ImmutableArray<MethodLineExtent> extents)
        {
            // sort by start line:
            var extentsOrderedByMinLine = extents.Sort(MethodLineExtent.MinLineComparer.Instance);

            // Most of the time method extents are non-overlapping. Only extents of anonymous methods and queries overlap methods and other lambdas.
            // The number of subsequences created below will be the max nesting level of lambdas.

            var subsequences = ImmutableArray.CreateBuilder<ImmutableArray<MethodLineExtent>.Builder>();

            foreach (var extent in extentsOrderedByMinLine)
            {
                bool placed = false;
                foreach (var subsequence in subsequences)
                {
                    if (subsequence.Count == 0 || extent.MinLine > subsequence[subsequence.Count - 1].MaxLine)
                    {
                        subsequence.Add(extent);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    var newRun = ImmutableArray.CreateBuilder<MethodLineExtent>();
                    newRun.Add(extent);
                    subsequences.Add(newRun);
                }
            }

            // make all subsequences immutable:

            var result = ImmutableArray.CreateBuilder<ImmutableArray<MethodLineExtent>>();
            foreach (var run in subsequences)
            {
                result.Add(run.ToImmutable());
            }

            return result.ToImmutable();
        }

        private static IEnumerable<(DocumentId, MethodLineExtent)> GetMethodExtents(PortablePdbReader pdbReader)
        {
            // Perf consideration (https://github.com/dotnet/symreader-portable/issues/46):
            // We read and decode all sequence points in the file, which might be megabytes of data that need to be paged in.
            // If we stored the primary document of single-document methods in a field of MethodBody table we would only need to decode 
            // sequence point of methods that span multiple documents to build a map from Document -> Methods.
            // We can then defer decoding sequence points of methods contained in a specified document until requested.

            foreach (var methodDebugHandle in pdbReader.MetadataReader.MethodDebugInformation)
            {
                foreach (var entry in GetMethodExtents(pdbReader, methodDebugHandle))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<(DocumentId, MethodLineExtent)> GetMethodExtents(
            PortablePdbReader pdbReader,
            MethodDebugInformationHandle methodDebugHandle, 
            ImmutableArray<int> lineDeltasOpt = default,
            int expectedSequencePointCount = -1)
        {
            var mdReader = pdbReader.MetadataReader;
            int version = pdbReader.Version;

            var debugInfo = mdReader.GetMethodDebugInformation(methodDebugHandle);

            if (debugInfo.SequencePointsBlob.IsNil)
            {
                yield break;
            }

            // sequence points:
            DocumentHandle currentDocument = debugInfo.Document;
            var methodId = pdbReader.GetMethodId(methodDebugHandle);

            int sequencePointIndex = 0;
            int minLine = int.MaxValue;
            int maxLine = int.MinValue;
            foreach (var sequencePoint in debugInfo.GetSequencePoints())
            {
                if (sequencePoint.IsHidden)
                {
                    sequencePointIndex++;
                    continue;
                }

                int startLine = sequencePoint.StartLine;
                int endLine = sequencePoint.EndLine;

                // apply delta:                :
                if (!lineDeltasOpt.IsDefault && sequencePointIndex < lineDeltasOpt.Length)
                {
                    int delta = lineDeltasOpt[sequencePointIndex];
                    unchecked
                    {
                        startLine += delta;
                        endLine += delta;
                    }
                }

                if (sequencePoint.Document != currentDocument)
                {
                    if (!currentDocument.IsNil)
                    {
                        yield return (pdbReader.GetDocumentId(currentDocument), new MethodLineExtent(methodId, version, minLine, maxLine));
                    }

                    currentDocument = sequencePoint.Document;
                    minLine = startLine;
                    maxLine = endLine;
                }
                else
                {
                    if (startLine < minLine)
                    {
                        minLine = startLine;
                    }

                    if (endLine > maxLine)
                    {
                        maxLine = endLine;
                    }
                }

                sequencePointIndex++;
            }

            // May be nil if a method spans multiple documents and all its sequence points are hidden.
            if (!currentDocument.IsNil)
            {
                yield return (pdbReader.GetDocumentId(currentDocument), new MethodLineExtent(methodId, version, minLine, maxLine));
            }

            if (expectedSequencePointCount >= 0 && sequencePointIndex != expectedSequencePointCount)
            {
                throw new InvalidInputDataException();
            }
        }

        /// <summary>
        /// Enumerates all documents that include at least one non-hidden sequence point of the specified method body.
        /// </summary>
        internal static (DocumentHandle Single, IEnumerable<DocumentHandle>? Multiple) GetMethodBodyDocuments(MetadataReader reader, MethodDebugInformationHandle handle)
        {
            var debugInfo = reader.GetMethodDebugInformation(handle);
            if (debugInfo.SequencePointsBlob.IsNil)
            {
                // no sequence points for the method:
                return (default, null);
            }

            if (!debugInfo.Document.IsNil)
            {
                // method body spans a single document:
                return (debugInfo.Document, null);
            }

            // method body spans multiple documents:
            return (default(DocumentHandle), Multiple());

            IEnumerable<DocumentHandle> Multiple()
            {
                var currentDocument = debugInfo.Document;

                foreach (var sequencePoint in debugInfo.GetSequencePoints())
                {
                    if (!sequencePoint.IsHidden && sequencePoint.Document != currentDocument)
                    {
                        if (!currentDocument.IsNil)
                        {
                            yield return currentDocument;
                        }

                        currentDocument = sequencePoint.Document;
                    }
                }

                // May be nil if a method spans multiple documents and all its sequence points are hidden.
                if (!currentDocument.IsNil)
                {
                    yield return currentDocument;
                }
            }
        }

        internal IEnumerable<(MethodId Id, int Version)>? GetMethodsContainingLine(DocumentId documentId, int line)
        {
            if (!TryGetPartitionedExtents(documentId, out var extentsByMinLine))
            {
                return null;
            }

            return EnumerateMethodsContainingLine(extentsByMinLine, line);
        }

        private static IEnumerable<(MethodId Id, int Version)> EnumerateMethodsContainingLine(ImmutableArray<ImmutableArray<MethodLineExtent>> extents, int line)
        {
            foreach (var subsequence in extents)
            {
                int index = IndexOfContainingExtent(subsequence, line, out var _);
                if (index >= 0)
                {
                    yield return (subsequence[index].Method, subsequence[index].Version);
                }
            }
        }

        private static int IndexOfContainingExtent(ImmutableArray<MethodLineExtent> orderedNonOverlappingExtents, int startLine, out int closestFollowingExtent)
        {
            closestFollowingExtent = -1;

            int index = orderedNonOverlappingExtents.BinarySearch(startLine, (extent, line) => extent.MinLine - line);
            if (index >= 0)
            {
                return index;
            }

            int preceding = ~index - 1;
            if (preceding >= 0 && startLine <= orderedNonOverlappingExtents[preceding].MaxLine)
            {
                return preceding;
            }

            closestFollowingExtent = ~index;
            return -1;
        }

        internal ImmutableArray<MethodLineExtent> GetMethodExtents(DocumentId documentId)
        {
            return _extentsByDocument.TryGetValue(documentId, out var result) ? result : ImmutableArray<MethodLineExtent>.Empty;
        }

        internal bool TryGetMethodSourceExtent(DocumentId documentId, MethodId methodId, out int startLine, out int endLine)
        {
            if (!_extentsByDocument.TryGetValue(documentId, out var extentsByMethod))
            {
                startLine = endLine = 0;
                return false;
            }

            int index = extentsByMethod.BinarySearch(methodId, (ext, id) => ext.Method.CompareTo(id));
            if (index < 0)
            {
                startLine = endLine = 0;
                return false;
            }

            var extent = extentsByMethod[index];
            startLine = extent.MinLine;
            endLine = extent.MaxLine;
            return true;
        }

        internal IEnumerable<MethodLineExtent> EnumerateContainingOrClosestFollowingMethodExtents(DocumentId documentId, int line)
        {
            if (!TryGetPartitionedExtents(documentId, out var extentsByMinLine))
            {
                yield break;
            }

            foreach (var subsequence in extentsByMinLine)
            {
                int closestFollowingExtent;
                int index = IndexOfContainingExtent(subsequence, line, out closestFollowingExtent);
                if (index >= 0)
                {
                    yield return subsequence[index];
                }
                else if (closestFollowingExtent < subsequence.Length)
                {
                    yield return subsequence[closestFollowingExtent];
                }
            }
        }
    }
}
