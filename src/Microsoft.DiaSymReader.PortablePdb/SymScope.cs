﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
#if NET9_0_OR_GREATER
using System.Runtime.InteropServices.Marshalling;
#endif

namespace Microsoft.DiaSymReader.PortablePdb
{
    [ComVisible(false)]
#if NET9_0_OR_GREATER
    [GeneratedComClass]
#endif
    public sealed partial class SymScope : ISymUnmanagedScope2
    {
        internal readonly ScopeData _data;

        internal SymScope(ScopeData data)
        {
            Debug.Assert(data != null);
            _data = data;
        }

        public int GetChildren(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedScope[] children)
        {
            var childrenData = _data.GetChildren();

            int i = 0;
            foreach (var childData in childrenData)
            {
                if (i >= bufferLength)
                {
                    break;
                }

                children[i++] = new SymScope(childData);
            }

            count = (bufferLength == 0) ? childrenData.Length : i;
            return HResult.S_OK;
        }

        public int GetConstantCount(out int count)
        {
            return GetConstants(0, out count, constants: null);
        }

        public int GetConstants(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedConstant[]? constants)
        {
            return _data.GetConstants(bufferLength, out count, constants);
        }

        public int GetLocalCount(out int count)
        {
            return GetLocals(0, out count, locals: null);
        }

        public int GetLocals(int bufferLength, out int count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedVariable[]? locals)
        {
            return _data.GetLocals(bufferLength, out count, locals);
        }

        public int GetMethod([MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedMethod method)
        {
            method = _data.SymMethod;
            return HResult.S_OK;
        }

        public int GetParent([MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedScope? scope)
        {
            var parentData = _data.Parent;
            scope = (parentData != null) ? new SymScope(parentData) : null;
            return HResult.S_OK;
        }

        public int GetStartOffset(out int offset)
        {
            offset = _data.StartOffset;
            return HResult.S_OK;
        }

        public int GetEndOffset(out int offset)
        {
            offset = _data.EndOffset;
            return HResult.S_OK;
        }

        public int GetNamespaces(
            int bufferLength,
            out int count,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0), Out]ISymUnmanagedNamespace[] namespaces)
        {
            // Language specific, the client has to use Portable PDB reader directly to access the data.
            // Pretend there are no namespace scopes.
            count = 0;
            return HResult.S_OK;
        }
    }
}
