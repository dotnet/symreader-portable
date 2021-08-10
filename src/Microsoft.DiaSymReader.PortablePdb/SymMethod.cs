// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace Microsoft.DiaSymReader.PortablePdb
{
    [ComVisible(false)]
    public sealed class SymMethod : ISymUnmanagedMethod2, ISymUnmanagedAsyncMethod, ISymEncUnmanagedMethod
    {
        internal MethodDebugInformationHandle DebugHandle { get; }
        internal MethodDefinitionHandle DefinitionHandle => DebugHandle.ToDefinitionHandle();
        internal PortablePdbReader PdbReader { get; }
        private RootScopeData _lazyRootScopeData;
        private AsyncMethodData _lazyAsyncMethodData;

        internal SymReader SymReader => PdbReader.SymReader;
        internal MetadataReader MetadataReader => PdbReader.MetadataReader;

        internal SymMethod(PortablePdbReader pdbReader, MethodDebugInformationHandle handle)
        {
            Debug.Assert(pdbReader != null);
            PdbReader = pdbReader;
            DebugHandle = handle;
        }

        internal MethodId GetId() => PdbReader.GetMethodId(DebugHandle);

        private SequencePointCollection GetSequencePoints() 
            => MetadataReader.GetMethodDebugInformation(DebugHandle).GetSequencePoints();

        internal StandaloneSignatureHandle GetLocalSignatureHandle() 
            => MetadataReader.GetMethodDebugInformation(DebugHandle).LocalSignature;

        private RootScopeData GetRootScopeData()
        {
            if (_lazyRootScopeData == null)
            {
                _lazyRootScopeData = new RootScopeData(this);
            }

            return _lazyRootScopeData;
        }

        private int GetILSize()
        {
            // SymWriter sets the size of the method to the end offset of the root scope in CloseMethod:
            return GetRootScopeData().EndOffset;
        }

        internal static int GetLocalVariableCount(MetadataReader metadataReader, MethodDebugInformationHandle handle)
        {
            int result = 0;
            foreach (var scopeHandle in metadataReader.GetLocalScopes(handle))
            {
                result += metadataReader.GetLocalScope(scopeHandle).GetLocalVariables().Count;
            }

            return result;
        }

        internal void AddLocalVariables(ISymUnmanagedVariable[] variables)
        {
            var metadataReader = MetadataReader;

            int i = 0;
            foreach (var scopeHandle in metadataReader.GetLocalScopes(DebugHandle))
            {
                var scope = metadataReader.GetLocalScope(scopeHandle);
                foreach (var variableHandle in scope.GetLocalVariables())
                {
                    variables[i++] = new SymVariable(this, variableHandle);
                }
            }
        }

        #region ISymUnmanagedMethod

        public int GetNamespace([MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedNamespace @namespace)
        {
            // SymReader doesn't support namespaces
            @namespace = null;
            return HResult.E_NOTIMPL;
        }

        public int GetOffset(ISymUnmanagedDocument document, int line, int column, out int offset)
        {
            if (line <= 0)
            {
                offset = 0;
                return HResult.E_INVALIDARG;
            }

            // Note that DiaSymReader completely ignores column parameter.

            var symDocument = SymReader.AsSymDocument(document);
            if (symDocument == null)
            {
                offset = 0;
                return HResult.E_INVALIDARG;
            }

            // DiaSymReader uses DiaSession::findLinesByLinenum, which results in bad results for lines shared across multiple methods
            // and for lines outside of the current method.

            var documentHandle = symDocument.Handle;

            if (!SymReader.TryGetLineDeltas(GetId(), out var deltas))
            {
                deltas = default(MethodLineDeltas);
            }

            int sequencePointIndex = 0;
            foreach (var sp in GetSequencePoints())
            {
                if (!sp.IsHidden && sp.Document == documentHandle)
                {
                    int delta = deltas.GetDeltaForSequencePoint(sequencePointIndex);
                    if (line >= sp.StartLine + delta && line <= sp.EndLine + delta)
                    {
                        // Return the first matching IL offset. In common cases there will be a single one 
                        // since sequence points of a single method don't overlap unless forced by #line.
                        offset = sp.Offset;
                        return HResult.S_OK;
                    }
                }

                sequencePointIndex++;
            }

            offset = 0;
            return HResult.E_FAIL;
        }

        public int GetParameters(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedVariable[] parameters)
        {
            // SymReader doesn't support parameter access. 
            count = 0;
            return HResult.E_NOTIMPL;
        }

        public int GetRanges(
            ISymUnmanagedDocument document,
            int line,
            int column,
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), Out]int[] ranges)
        {
            if (line <= 0)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            // Note that DiaSymReader completely ignores column parameter.

            var symDocument = SymReader.AsSymDocument(document);
            if (symDocument == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            // DiaSymReader uses DiaSession::findLinesByLinenum, which results in bad results for lines shared across multiple methods.

            var documentHandle = symDocument.Handle;

            if (!SymReader.TryGetLineDeltas(GetId(), out var deltas))
            {
                deltas = default(MethodLineDeltas);
            }

            bool setEndOffset = false;
            int i = 0;
            int sequencePointIndex = 0;
            foreach (var sp in GetSequencePoints())
            {
                if (setEndOffset)
                {
                    ranges[i - 1] = sp.Offset;
                    setEndOffset = false;
                }

                if (!sp.IsHidden && sp.Document == documentHandle)
                {
                    int delta = deltas.GetDeltaForSequencePoint(sequencePointIndex);
                    if (line >= sp.StartLine + delta && line <= sp.EndLine + delta)
                    {
                        if (i + 1 < bufferLength)
                        {
                            ranges[i] = sp.Offset;
                            setEndOffset = true;
                        }

                        // pair of offsets for each sequence point
                        i += 2;
                    }
                }

                sequencePointIndex++;
            }

            if (setEndOffset)
            {
                ranges[i - 1] = GetILSize();
            }

            count = i;
            return HResult.S_OK;
        }

        public int GetRootScope([MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedScope scope)
        {
            // SymReader always creates a new scope instance
            scope = new SymScope(GetRootScopeData());
            return HResult.S_OK;
        }

        public int GetScopeFromOffset(int offset, [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedScope scope)
        {
            // SymReader doesn't support. 
            scope = null;
            return HResult.S_OK;
        }

        public int GetSequencePointCount(out int count)
        {
            int i = 0;
            foreach (var sp in GetSequencePoints())
            {
                i++;
            }

            count = i;
            return HResult.S_OK;
        }

        public int GetSequencePoints(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]int[] offsets,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedDocument[] documents,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]int[] startLines,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]int[] startColumns,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]int[] endLines,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]int[] endColumns)
        {
            SymDocument currentDocument = null;

            if ((startLines == null && endLines == null) || !SymReader.TryGetLineDeltas(GetId(), out var deltas))
            {
                deltas = default(MethodLineDeltas);
            }

            int i = 0;
            foreach (var sp in GetSequencePoints())
            {
                if (bufferLength != 0 && i >= bufferLength)
                {
                    break;
                }

                int delta = sp.IsHidden ? 0 : deltas.GetDeltaForSequencePoint(i);

                if (offsets != null)
                {
                    offsets[i] = sp.Offset;
                }

                if (startLines != null)
                {
                    startLines[i] = sp.StartLine + delta;
                }

                if (startColumns != null)
                {
                    startColumns[i] = sp.StartColumn;
                }

                if (endLines != null)
                {
                    endLines[i] = sp.EndLine + delta;
                }

                if (endColumns != null)
                {
                    endColumns[i] = sp.EndColumn;
                }

                if (documents != null)
                {
                    if (currentDocument == null || currentDocument.Handle != sp.Document)
                    {
                        currentDocument = new SymDocument(PdbReader, sp.Document);
                    }

                    documents[i] = currentDocument;
                }

                i++;
            }

            count = i;
            return HResult.S_OK;
        }

        public int GetSourceStartEnd(
            ISymUnmanagedDocument[] documents,
            [In, MarshalAs(UnmanagedType.LPArray), Out]int[] lines,
            [In, MarshalAs(UnmanagedType.LPArray), Out]int[] columns,
            out bool defined)
        {
            // This symbol reader doesn't support source start/end for methods.
            defined = false;
            return HResult.E_NOTIMPL;
        }

        public int GetToken(out int methodToken)
        {
            methodToken = PdbReader.GetMethodId(DebugHandle).Token;
            return HResult.S_OK;
        }

        #endregion

        #region ISymUnmanagedMethod2

        /// <summary>
        /// Get the token of the local signature.
        /// </summary>
        /// <param name="localSignatureToken">Local signature token (StandAloneSig), or 0 if the method doesn't have any local variables.</param>
        /// <returns>
        /// S_OK if the method has a local signature,
        /// S_FALSE if the method doesn't have a local signature, 
        /// E_* if an error occurs while reading the signature.
        /// </returns>
        public int GetLocalSignatureToken(out int localSignatureToken)
        {
            var handle = GetLocalSignatureHandle();
            if (handle.IsNil)
            {
                localSignatureToken = 0;
                return HResult.S_FALSE;
            }

            localSignatureToken =  MetadataTokens.GetToken(handle);
            return HResult.S_OK;
        }
        
        #endregion

        #region ISymUnmanagedAsyncMethod

        private AsyncMethodData AsyncMethodData
        {
            get
            {
                if (_lazyAsyncMethodData == null)
                {
                    _lazyAsyncMethodData = ReadAsyncMethodData();
                }

                return _lazyAsyncMethodData;
            }
        }

        private AsyncMethodData ReadAsyncMethodData()
        {
            var reader = MetadataReader;
            var body = reader.GetMethodDebugInformation(DebugHandle);
            var kickoffMethod = body.GetStateMachineKickoffMethod();

            if (kickoffMethod.IsNil)
            {
                return AsyncMethodData.None;
            }

            var value = reader.GetCustomDebugInformation(DefinitionHandle, MetadataUtilities.MethodSteppingInformationBlobId);
            if (value.IsNil)
            {
                return AsyncMethodData.None;
            }

            var blobReader = reader.GetBlobReader(value);

            long catchHandlerOffset = blobReader.ReadUInt32();
            if (catchHandlerOffset > (uint)int.MaxValue + 1)
            {
                throw new BadImageFormatException();
            }

            var yieldOffsets = ImmutableArray.CreateBuilder<int>();
            var resultOffsets = ImmutableArray.CreateBuilder<int>();
            var resumeMethods = ImmutableArray.CreateBuilder<int>();

            while (blobReader.RemainingBytes > 0)
            {
                uint yieldOffset = blobReader.ReadUInt32();
                if (yieldOffset > int.MaxValue)
                {
                    throw new BadImageFormatException();
                }

                uint resultOffset = blobReader.ReadUInt32();
                if (resultOffset > int.MaxValue)
                {
                    throw new BadImageFormatException();
                }

                yieldOffsets.Add((int)yieldOffset);
                resultOffsets.Add((int)resultOffset);
                resumeMethods.Add(MetadataUtilities.MethodDefToken(blobReader.ReadCompressedInteger()));
            }

            return new AsyncMethodData(
                kickoffMethod,
                (int)(catchHandlerOffset - 1),
                yieldOffsets.ToImmutable(),
                resultOffsets.ToImmutable(),
                resumeMethods.ToImmutable());
        }

        public int IsAsyncMethod(out bool value)
        {
            value = !AsyncMethodData.IsNone;
            return HResult.S_OK;
        }

        public int GetKickoffMethod(out int kickoffMethodToken)
        {
            if (AsyncMethodData.IsNone)
            {
                kickoffMethodToken = 0;
                return HResult.E_UNEXPECTED;
            }

            kickoffMethodToken = MetadataTokens.GetToken(AsyncMethodData.KickoffMethod);
            return HResult.S_OK;
        }

        public int HasCatchHandlerILOffset(out bool value)
        {
            if (AsyncMethodData.IsNone)
            {
                value = false;
                return HResult.E_UNEXPECTED;
            }

            value = AsyncMethodData.CatchHandlerOffset >= 0;
            return HResult.S_OK;
        }

        public int GetCatchHandlerILOffset(out int offset)
        {
            if (AsyncMethodData.IsNone || AsyncMethodData.CatchHandlerOffset < 0)
            {
                offset = 0;
                return HResult.E_UNEXPECTED;
            }

            offset = AsyncMethodData.CatchHandlerOffset;
            return HResult.S_OK;
        }

        public int GetAsyncStepInfoCount(out int count)
        {
            if (AsyncMethodData.IsNone)
            {
                count = 0;
                return HResult.E_UNEXPECTED;
            }

            count = AsyncMethodData.YieldOffsets.Length;
            return HResult.S_OK;
        }

        public int GetAsyncStepInfo(
            int bufferLength,
            out int count,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]int[] yieldOffsets,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]int[] breakpointOffsets,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]int[] breakpointMethods)
        {
            if (AsyncMethodData.IsNone)
            {
                count = 0;
                return HResult.E_UNEXPECTED;
            }

            int length = Math.Min(bufferLength, AsyncMethodData.YieldOffsets.Length);

            if (yieldOffsets != null)
            {
                AsyncMethodData.YieldOffsets.CopyTo(0, yieldOffsets, 0, length);
            }

            if (breakpointOffsets != null)
            {
                AsyncMethodData.ResumeOffsets.CopyTo(0, breakpointOffsets, 0, length);
            }

            if (breakpointMethods != null)
            {
                AsyncMethodData.ResumeMethods.CopyTo(0, breakpointMethods, 0, length);
            }

            count = length;
            return HResult.S_OK;
        }

        #endregion

        #region ISymEncUnmanagedMethod

        /// <summary>
        /// Get the file name for the line associated with specified offset.
        /// </summary>
        public int GetFileNameFromOffset(
            int offset,
            int bufferLength,
            out int count,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] name)
        {
            if (offset < 0)
            {
                offset = int.MaxValue;
            }

            var debugInfo = MetadataReader.GetMethodDebugInformation(DebugHandle);
            var candidate = debugInfo.Document;

            if (candidate.IsNil)
            {
                // method body spans multiple documents:
                foreach (var sp in GetSequencePoints())
                {
                    if (sp.Offset <= offset)
                    {
                        candidate = sp.Document;
                    }
                    else if (sp.Offset > offset)
                    {
                        break;
                    }
                }

                // offset is negative, or sequence point blob is missing document definition (bad metadata):
                if (candidate.IsNil)
                {
                    count = 0;
                    return HResult.E_FAIL;
                }
            }

            var document = MetadataReader.GetDocument(candidate);
            string nameStr = MetadataReader.GetString(document.Name);
            return InteropUtilities.StringToBuffer(nameStr, bufferLength, out count, name);
        }

        /// <summary>
        /// Get the line information associated with <paramref name="offset"/>.
        /// </summary>
        /// <remarks>
        /// If <paramref name="offset"/> is not a sequence point it is associated with the previous one.
        /// <paramref name="sequencePointOffset"/> provides the associated sequence point.
        /// </remarks>
        public int GetLineFromOffset(
            int offset,
            out int startLine,
            out int startColumn,
            out int endLine,
            out int endColumn,
            out int sequencePointOffset)
        {
            if (offset < 0)
            {
                offset = int.MaxValue;
            }

            var candidate = default(SequencePoint);
            int candidateIndex = -1;

            int sequencePointIndex = 0;
            foreach (var sp in GetSequencePoints())
            {
                if (sp.Offset <= offset)
                {
                    candidate = sp;
                    candidateIndex = sequencePointIndex;
                }
                else if (sp.Offset > offset)
                {
                    break;
                }

                sequencePointIndex++;
            }

            if (candidateIndex < 0)
            {
                startLine = startColumn = endLine = endColumn = sequencePointOffset = 0;
                return HResult.E_FAIL;
            }

            int delta = !candidate.IsHidden && SymReader.TryGetLineDeltas(GetId(), out var deltas) ? 
                deltas.GetDeltaForSequencePoint(candidateIndex) : 0;

            startLine = candidate.StartLine + delta;
            startColumn = candidate.StartColumn;
            endLine = candidate.EndLine + delta;
            endColumn = candidate.EndColumn;
            sequencePointOffset = candidate.Offset;
            return HResult.S_OK;
        }

        /// <summary>
        /// Get the number of Documents that this method has lines in.
        /// </summary>
        public int GetDocumentsForMethodCount(out int count)
        {
            int hr = GetDocumentsForMethod(0, out count, EmptyArray<ISymUnmanagedDocument>.Instance);
            return (hr != HResult.E_INVALIDARG) ? hr : HResult.S_OK;
        }

        /// <summary>
        /// Get the documents this method has lines in.
        /// </summary>
        public int GetDocumentsForMethod(
            int bufferLength,
            out int count,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]ISymUnmanagedDocument[] documents)
        {
            if (documents == null)
            {
                count = 0;
                return HResult.E_INVALIDARG;
            }

            var (single, multiple) = MethodExtents.GetMethodBodyDocuments(MetadataReader, DebugHandle);
            if (!single.IsNil)
            {
                count = 1;

                if (documents.Length < 1)
                {
                    return HResult.E_INVALIDARG;
                }

                documents[0] = new SymDocument(PdbReader, single);
                return HResult.S_OK;
            }

            // SymMethod w/o debug info wouldn't be created:
            Debug.Assert(multiple != null);

            // Methods don't usually span too many documents, so it's ok to use linear search.
            var uniqueHandles = new List<DocumentHandle>();
            foreach (var documentHandle in multiple)
            {
                if (!uniqueHandles.Contains(documentHandle))
                {
                    uniqueHandles.Add(documentHandle);
                }
            }

            count = uniqueHandles.Count;

            if (documents.Length < uniqueHandles.Count)
            {
                return HResult.E_INVALIDARG;
            }

            for (int i = 0; i < uniqueHandles.Count; i++)
            {
                documents[i] = new SymDocument(PdbReader, uniqueHandles[i]);
            }

            return HResult.S_OK;
        }

        /// <summary>
        /// Get the smallest start line and largest end line, for the method, in a specific document.
        /// </summary>
        public int GetSourceExtentInDocument(ISymUnmanagedDocument document, out int startLine, out int endLine)
        {
            return PdbReader.GetMethodSourceExtentInDocument(document, this, out startLine, out endLine);
        }

        #endregion
    }
}
