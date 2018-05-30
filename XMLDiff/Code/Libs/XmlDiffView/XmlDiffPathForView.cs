//------------------------------------------------------------------------------
// <copyright file="XmlDiffPathForView.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Xml;
using System.Diagnostics;
using System.Collections;

namespace Microsoft.XmlDiffPatch
{
	internal class XmlDiffPath
	{
        static char[] Delimites = new char[] {'|','-','/'};
        static char[] MultiNodesDelimiters = new char[] {'|','-'};

        internal static XmlDiffPathNodeList SelectNodes( XmlDiffViewParentNode rootNode, XmlDiffViewParentNode currentParentNode, string xmlDiffPathExpr )
        {
            switch ( xmlDiffPathExpr[0] )
            {
                case '/':
                    return SelectAbsoluteNodes( rootNode, xmlDiffPathExpr );
                case '@':
                    if ( xmlDiffPathExpr.Length < 2 )
                        OnInvalidExpression( xmlDiffPathExpr );
                    if ( xmlDiffPathExpr[1] == '*' )
                        return SelectAllAttributes( (XmlDiffViewElement)currentParentNode );
                    else
                        return SelectAttributes( (XmlDiffViewElement)currentParentNode, xmlDiffPathExpr );
                case '*':
                    if ( xmlDiffPathExpr.Length == 1 )
                        return SelectAllChildren( currentParentNode );
                    else 
                    {
                        OnInvalidExpression( xmlDiffPathExpr );
                        return null;
                    }
                default:
                    return SelectChildNodes( currentParentNode, xmlDiffPathExpr, 0 );
            }
        }

        static XmlDiffPathNodeList SelectAbsoluteNodes( XmlDiffViewParentNode rootNode, string path )
        {
            Debug.Assert( path[0] == '/' );
            
            int pos = 1;
            XmlDiffViewNode node = rootNode;

            for (;;)
            {
                int startPos = pos;
                int nodePos = ReadPosition( path, ref pos );

                if ( pos == path.Length || path[pos] == '/' ) {
                    if ( node.FirstChildNode == null ) {
                        OnNoMatchingNode( path );
                    }

                    XmlDiffViewParentNode parentNode = (XmlDiffViewParentNode) node;
                    if ( nodePos <= 0 || nodePos > parentNode._sourceChildNodesCount ) {
                        OnNoMatchingNode( path );
                    }

                    node = parentNode.GetSourceChildNode( nodePos - 1 );

                    if ( pos == path.Length ) {
                        XmlDiffPathNodeList list = new XmlDiffPathSingleNodeList();
                        list.AddNode( node );
                        return list;
                    }
                     
                    pos++;
                }
                else {
                    if ( path[pos] == '-' || path[pos] == '|' ) {
                        if ( node.FirstChildNode == null ) {
                            OnNoMatchingNode( path );
                        }
                        return SelectChildNodes( ((XmlDiffViewParentNode)node), path, startPos );
                    }

                    OnInvalidExpression( path );
                }
            }
        }

        static XmlDiffPathNodeList SelectAllAttributes( XmlDiffViewElement parentElement )
        {
            if ( parentElement._attributes == null ) 
            {
                OnNoMatchingNode( "@*" );
                return null;
            }
            else if ( parentElement._attributes._nextSibbling == null ) 
            {
                XmlDiffPathNodeList nodeList = new XmlDiffPathSingleNodeList();
                nodeList.AddNode( parentElement._attributes );
                return nodeList;
            }
            else 
            {
                XmlDiffPathNodeList nodeList = new XmlDiffPathMultiNodeList();
                XmlDiffViewAttribute curAttr = parentElement._attributes;
                while ( curAttr != null )
                    nodeList.AddNode( curAttr );
                return nodeList;
            }
        }

        static XmlDiffPathNodeList SelectAttributes( XmlDiffViewElement parentElement, string path )
        {
            Debug.Assert( path[0] == '@' );

            int pos = 1;
            XmlDiffPathNodeList nodeList = null;
            for (;;) 
            {
                string name = ReadAttrName( path, ref pos );

                if ( nodeList == null ) 
                {
                    if ( pos == path.Length ) 
                        nodeList = new XmlDiffPathSingleNodeList();
                    else
                        nodeList = new XmlDiffPathMultiNodeList();
                }

                XmlDiffViewAttribute attr = parentElement.GetAttribute( name );
                if ( attr == null )
                    OnNoMatchingNode( path );

                nodeList.AddNode( attr );

                if ( pos == path.Length )
                    break;
                else if ( path[pos] == '|' ) {
                    pos++;
                    if ( path[pos] != '@' )
                        OnInvalidExpression( path );
                    pos++;
                }
                else
                    OnInvalidExpression( path );
            }

            return nodeList;
        }

        static XmlDiffPathNodeList SelectAllChildren( XmlDiffViewParentNode parentNode )
        {
            if ( parentNode._childNodes == null ) 
            {
                OnNoMatchingNode( "*" );
                return null;
            }
            else if ( parentNode._childNodes._nextSibbling == null ) 
            {
                XmlDiffPathNodeList nodeList = new XmlDiffPathSingleNodeList();
                nodeList.AddNode( parentNode._childNodes );
                return nodeList;
            }
            else 
            {
                XmlDiffPathNodeList nodeList = new XmlDiffPathMultiNodeList();
                XmlDiffViewNode childNode = parentNode._childNodes;
                while ( childNode != null ) {
                    nodeList.AddNode( childNode );
                    childNode = childNode._nextSibbling;
                }
                return nodeList;
            }   
        }

        static XmlDiffPathNodeList SelectChildNodes( XmlDiffViewParentNode parentNode, string path, int startPos )
        {
            int pos = startPos;
            XmlDiffPathNodeList nodeList = null;

            for (;;)
            {
                int nodePos = ReadPosition( path, ref pos );

                if ( pos == path.Length ) 
                    nodeList = new XmlDiffPathSingleNodeList();
                else
                    nodeList = new XmlDiffPathMultiNodeList();

                if ( nodePos <= 0 || nodePos > parentNode._sourceChildNodesCount )
                    OnNoMatchingNode( path );

                nodeList.AddNode( parentNode.GetSourceChildNode( nodePos-1 ) );

                if ( pos == path.Length )
                    break;
                else if ( path[pos] == '|' )
                    pos++;
                else if ( path[pos] == '-' )
                {
                    pos++;
                    int endNodePos = ReadPosition( path, ref pos );
                    if ( endNodePos <= 0 || endNodePos > parentNode._sourceChildNodesCount )
                        OnNoMatchingNode( path );

                    while ( nodePos < endNodePos )
                    {
                        nodePos++;
                        nodeList.AddNode( parentNode.GetSourceChildNode( nodePos-1 ) );
                    }

                    if ( pos == path.Length )
                        break;
                    else if ( path[pos] == '|' )
                        pos++;
                    else 
                        OnInvalidExpression( path );
                }
            }
            return nodeList;
        }

        static int ReadPosition( string str, ref int pos ) 
        {
            int end = str.IndexOfAny( Delimites, pos );
            if ( end < 0 )
                end = str.Length;
            
            // TODO: better error handling if this should be shipped
            int nodePos = int.Parse( str.Substring( pos, end - pos ) );

            pos = end;
            return nodePos;
        }

        static string ReadAttrName( string str, ref int pos ) 
        {
            int end = str.IndexOf( '|', pos );
            if ( end < 0 )
                end = str.Length;
            
            // TODO: better error handling if this should be shipped
            string name = str.Substring( pos, end - pos );

            pos = end;
            return name;
        }

        static void OnInvalidExpression( string path )
        {
            throw new Exception( "Invalid XmlDiffPath expression: " + path );
        }

        static void OnNoMatchingNode( string path )
        {
            throw new Exception( "No matching node:" + path );
        }

	}


//////////////////////////////////////////////////////////////////
// XmlDiffPathNodeList
//
internal abstract class XmlDiffPathNodeList
{
    internal abstract void AddNode( XmlDiffViewNode node );
    internal abstract void Reset();
    internal abstract XmlDiffViewNode Current { get; }
    internal abstract bool MoveNext();
    internal abstract int Count { get; }
}

//////////////////////////////////////////////////////////////////
// XmlDiffPathNodeList
//
internal class XmlDiffPathMultiNodeList : XmlDiffPathNodeList
{
    internal class ListChunk 
    {
        internal const int ChunkSize = 10;

        internal XmlDiffViewNode[] _nodes = new XmlDiffViewNode[ ChunkSize ];
        internal int _count = 0;
        internal ListChunk _next = null;

        internal XmlDiffViewNode this[int i] { get { return _nodes[i]; } }

        internal void AddNode( XmlDiffViewNode node )
        {
            Debug.Assert( _count < ChunkSize );
            _nodes[ _count++ ] = node;
        }
    }

// Fields
    int _count = 0;
    ListChunk _chunks = null;
    ListChunk _lastChunk = null;
    ListChunk _currentChunk = null;
    int _currentChunkIndex = -1;

// Constructor
	internal XmlDiffPathMultiNodeList()
	{
	}

    internal override XmlDiffViewNode Current
    {
        get 
        { 
            if ( _currentChunk == null || _currentChunkIndex < 0)
                return null;
            else
                return _currentChunk[ _currentChunkIndex ];
        }
    }

    internal override int Count 
    { 
        get { return _count; } 
    }

// Methods
    internal override bool MoveNext()
    {
        if ( _currentChunk == null )
            return false;

        if ( _currentChunkIndex >= _currentChunk._count - 1 )
        {
            if ( _currentChunk._next == null )
                return false;
            else
            {
                _currentChunk = _currentChunk._next;
                _currentChunkIndex = 0;
                Debug.Assert( _currentChunk._count > 0 );
                return true;
            }
        }
        else
        {
            _currentChunkIndex++;
            return true;
        }
    }

    internal override void Reset()
    {
        _currentChunk = _chunks;
        _currentChunkIndex = -1;
    }

    internal override void AddNode( XmlDiffViewNode node )
    {
        if ( _lastChunk == null )
        {
            _chunks = new ListChunk();
            _lastChunk = _chunks;
            _currentChunk = _chunks;
        }
        else if ( _lastChunk._count == ListChunk.ChunkSize )
        {
            _lastChunk._next = new ListChunk();
            _lastChunk = _lastChunk._next;
        }

        _lastChunk.AddNode( node );
        _count++;
    }
}

//////////////////////////////////////////////////////////////////
// XmlDiffPathSingleNodeList
//
internal class XmlDiffPathSingleNodeList : XmlDiffPathNodeList
{
    enum State 
    { 
        BeforeNode = 0,
        OnNode = 1,
        AfterNode = 2
    }

    XmlDiffViewNode _node;
    State _state = State.BeforeNode;

	internal XmlDiffPathSingleNodeList ()
	{
	}

    internal override int Count 
    { 
        get { return 1; } 
    }

    internal override XmlDiffViewNode Current 
    {
        get { return ( _state == State.OnNode ) ? _node : null; } 
    }

    internal override bool MoveNext()
    {
        switch ( _state ) 
        {
            case State.BeforeNode:
                _state = State.OnNode;
                return true;
            case State.OnNode:
                _state = State.AfterNode;
                return false;
            case State.AfterNode:
                return false;
            default:
                return false;
        }
    }

    internal override void Reset()
    {
        _state = State.BeforeNode;
    }

    internal override void AddNode( XmlDiffViewNode node )
    {
        if ( _node != null )
            throw new Exception( "XmlDiffPathSingleNodeList can contain one node only." );
        _node = node;
    }
}
}


