//------------------------------------------------------------------------------
// <copyright file="OperationDescriptor.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;

namespace Microsoft.XmlDiffPatch
{
	internal class OperationDescriptor
	{
        internal enum Type {
            Move,
            NamespaceChange,
            PrefixChange,
        }

        int _opid;
        internal Type _type;
        internal XmlDiffPathMultiNodeList _nodeList;

		public OperationDescriptor( int opid, Type type )
		{
            _opid = opid;
            _type = type;
             _nodeList = new XmlDiffPathMultiNodeList();
		}
	}
}
