//------------------------------------------------------------------------------
// <copyright file="XmlDiffView.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Collections;


namespace Microsoft.XmlDiffPatch {

internal enum XmlDiffViewOperation {
    Match    = 0,
    Ignore   = 1,
    Add      = 2,
    MoveTo   = 3,
    Remove   = 4,
    MoveFrom = 5,
    Change   = 6,
}

public sealed class XmlDiffView {

    XmlDiffViewDocument _doc = null;
    Hashtable _descriptors = new Hashtable();

    // options
    bool _bIgnoreChildOrder  = false;
    bool _bIgnoreComments    = false;
    bool _bIgnorePI          = false;
    bool _bIgnoreWhitespace  = false;
    bool _bIgnoreNamespaces  = false;
    bool _bIgnorePrefixes    = false;
    bool _bIgnoreXmlDecl     = false;
    bool _bIgnoreDtd         = false;

    // used when loading
    struct LoadState {
        public XmlDiffViewNode _curLastChild;
        public XmlDiffViewAttribute _curLastAttribute;

        public void Reset() {
            _curLastChild = null;
            _curLastAttribute = null;
        }
    }
    LoadState _loadState;

    public XmlDiffView() {
	}

    public void Load( XmlReader sourceXml, XmlReader diffgram ) {
        // load diffgram to DOM
        XmlDocument diffgramDoc = new XmlDocument();
        diffgramDoc.Load( diffgram );

        // process operation descriptors
        PreprocessDiffgram( diffgramDoc );

        // load document
        _doc = new XmlDiffViewDocument();
        LoadSourceChildNodes( _doc, sourceXml, false );

        // apply diffgram
        ApplyDiffgram( diffgramDoc.DocumentElement, _doc );
    }

    private void PreprocessDiffgram( XmlDocument diffgramDoc ) {
        // read xmldiff options
        XmlAttribute attr = (XmlAttribute)diffgramDoc.DocumentElement.Attributes.GetNamedItem( "options" );
        if ( attr == null ) 
            throw new Exception( "Missing 'options' attribute in the diffgram.");
        string optionsAttr = attr.Value;
        XmlDiffOptions options = XmlDiff.ParseOptions( optionsAttr );
        _bIgnoreChildOrder = ( ( (int)options & (int)(XmlDiffOptions.IgnoreChildOrder) ) > 0 ) ;
        _bIgnoreComments   = ( ( (int)options & (int)(XmlDiffOptions.IgnoreComments)   ) > 0 ) ;
        _bIgnorePI         = ( ( (int)options & (int)(XmlDiffOptions.IgnorePI)         ) > 0 ) ;
        _bIgnoreWhitespace = ( ( (int)options & (int)(XmlDiffOptions.IgnoreWhitespace) ) > 0 ) ;
        _bIgnoreNamespaces = ( ( (int)options & (int)(XmlDiffOptions.IgnoreNamespaces) ) > 0 ) ;
        _bIgnorePrefixes   = ( ( (int)options & (int)(XmlDiffOptions.IgnorePrefixes)   ) > 0 ) ;
        _bIgnoreDtd        = ( ( (int)options & (int)(XmlDiffOptions.IgnoreDtd)        ) > 0 ) ;

        if ( _bIgnoreNamespaces )
            _bIgnorePrefixes = true;

        // read descriptors
        XmlNodeList children = diffgramDoc.DocumentElement.ChildNodes;
        IEnumerator e = children.GetEnumerator();
        while ( e.MoveNext() ) {
            XmlElement desc = e.Current as XmlElement;
            if ( desc != null && desc.LocalName == "descriptor" ) {
                int opid = int.Parse( desc.GetAttribute( "opid" ) );
                OperationDescriptor.Type type;
                switch ( desc.GetAttribute( "type" ) ) {
                    case "move":             type = OperationDescriptor.Type.Move; break;
                    case "prefix change":    type = OperationDescriptor.Type.PrefixChange; break;
                    case "namespace change": type = OperationDescriptor.Type.NamespaceChange; break;
                    default:
                        throw new Exception( "Invalid descriptor type." );
                }
                OperationDescriptor od = new OperationDescriptor( opid, type );
                _descriptors[opid] = od;
            }
        }
    }

    private void LoadSourceChildNodes( XmlDiffViewParentNode parent, XmlReader reader, bool bEmptyElement ) {
        LoadState savedLoadState = _loadState;
        _loadState.Reset();

        // load attributes
        while ( reader.MoveToNextAttribute() )
        {
            XmlDiffViewAttribute attr;
            if ( reader.Prefix == "xmlns" ||
                ( reader.Prefix == string.Empty  && reader.LocalName == "xmlns" ))
            {
                attr = new XmlDiffViewAttribute( reader.LocalName, reader.Prefix, reader.NamespaceURI, reader.Value );
                if ( _bIgnoreNamespaces ) 
                    attr._op = XmlDiffViewOperation.Ignore;
            }
            else
            {
                string attrValue = _bIgnoreWhitespace ? NormalizeText( reader.Value ) : reader.Value;
                attr = new XmlDiffViewAttribute( reader.LocalName, reader.Prefix, reader.NamespaceURI, attrValue );
            }
            ((XmlDiffViewElement)parent).InsertAttributeAfter( attr, _loadState._curLastAttribute );
            _loadState._curLastAttribute = attr;
        }

        // empty element -> return, do not load chilren
        if ( bEmptyElement ) 
            goto End;

        // load children
        while ( reader.Read() ) {
            // ignore whitespaces between nodes
            if ( reader.NodeType == XmlNodeType.Whitespace )
                continue;

            XmlDiffViewNode child = null;
            switch ( reader.NodeType ) 
            {
                case XmlNodeType.Element:
                    bool bEmptyEl = reader.IsEmptyElement;
                    XmlDiffViewElement elem = new XmlDiffViewElement( reader.LocalName, reader.Prefix, reader.NamespaceURI, _bIgnorePrefixes );
                    LoadSourceChildNodes( elem, reader, bEmptyEl );
                    child = elem;
                    break;
                case XmlNodeType.Attribute:
                    Debug.Assert( false, "We should never get to this point, attributes should be read at the beginning of thid method." );
                    break;
                case XmlNodeType.Text:
                    child = new XmlDiffViewCharData(( _bIgnoreWhitespace ) ? NormalizeText( reader.Value ) : reader.Value, XmlNodeType.Text );
                    break;
                case XmlNodeType.CDATA:
                    child = new XmlDiffViewCharData( reader.Value, XmlNodeType.CDATA );
                    break;
                case XmlNodeType.EntityReference:
                    child = new XmlDiffViewER( reader.Name );
                    break;
                case XmlNodeType.Comment:
                    child = new XmlDiffViewCharData( reader.Value, XmlNodeType.Comment );
                    if ( _bIgnoreComments ) 
                        child._op = XmlDiffViewOperation.Ignore;
                    break;
                case XmlNodeType.ProcessingInstruction:
                    child = new XmlDiffViewPI( reader.Name, reader.Value );
                    if ( _bIgnorePI )
                        child._op = XmlDiffViewOperation.Ignore;
                    break;
                case XmlNodeType.SignificantWhitespace:
                    if( reader.XmlSpace == XmlSpace.Preserve ) {
                        child = new XmlDiffViewCharData( reader.Value, XmlNodeType.SignificantWhitespace );
                        if ( _bIgnoreWhitespace ) 
                            child._op = XmlDiffViewOperation.Ignore;
                    }
                    break;
                case XmlNodeType.XmlDeclaration:
                    child = new XmlDiffViewXmlDeclaration( NormalizeText( reader.Value ));
                    if ( _bIgnoreXmlDecl ) 
                        child._op = XmlDiffViewOperation.Ignore;
                    break;
                case XmlNodeType.EndElement:
                    goto End;

                case XmlNodeType.DocumentType:
                    child = new XmlDiffViewDocumentType( reader.Name, reader.GetAttribute("PUBLIC"),reader.GetAttribute("SYSTEM"), reader.Value );
                    if ( _bIgnoreDtd )
                        child._op = XmlDiffViewOperation.Ignore;
                    break;

                default:
                    Debug.Assert( false, "Invalid node type" );
                    break;
            }
            parent.InsertChildAfter( child, _loadState._curLastChild, true );
            _loadState._curLastChild = child;
        }

    End:
        _loadState = savedLoadState;
    }

    private void ApplyDiffgram( XmlNode diffgramParent, XmlDiffViewParentNode sourceParent ) {
        
        sourceParent.CreateSourceNodesIndex();
        XmlDiffViewNode currentPosition = null;

        IEnumerator diffgramChildren = diffgramParent.ChildNodes.GetEnumerator();
        while ( diffgramChildren.MoveNext() ) {
            XmlNode diffgramNode = (XmlNode)diffgramChildren.Current;
            if ( diffgramNode.NodeType == XmlNodeType.Comment )
                continue;
            XmlElement diffgramElement = diffgramChildren.Current as XmlElement;
            if ( diffgramElement == null )
                throw new Exception( "Invalid node in diffgram." );

            if ( diffgramElement.NamespaceURI != XmlDiff.NamespaceUri )
                throw new Exception( "Invalid element in diffgram." );

            string matchAttr = diffgramElement.GetAttribute( "match" );
            XmlDiffPathNodeList matchNodes = null;
            if ( matchAttr != string.Empty )
                matchNodes = XmlDiffPath.SelectNodes( _doc, sourceParent, matchAttr );

            switch ( diffgramElement.LocalName ) {
                case "node":
                    if ( matchNodes.Count != 1 )
                        throw new Exception( "The 'match' attribute of 'node' element must select a single node." );
                    matchNodes.MoveNext();
                    if ( diffgramElement.ChildNodes.Count > 0 )
                        ApplyDiffgram( diffgramElement, (XmlDiffViewParentNode)matchNodes.Current );
                    currentPosition = matchNodes.Current;
                    break;
                case "add":
                    if ( matchAttr != string.Empty ) {
                        OnAddMatch( diffgramElement, matchNodes, sourceParent, ref currentPosition );
                    }
                    else {
                        string typeAttr = diffgramElement.GetAttribute( "type" );
                        if ( typeAttr != string.Empty ) {
                            OnAddNode( diffgramElement, typeAttr, sourceParent, ref currentPosition );
                        }
                        else {
                            OnAddFragment( diffgramElement, sourceParent, ref currentPosition );
                        }
                    }
                    break;
                case "remove":
                    OnRemove( diffgramElement, matchNodes, sourceParent, ref currentPosition );
                    break;
                case "change":
                    OnChange( diffgramElement, matchNodes, sourceParent, ref currentPosition );
                    break;
            }
        }
    }

    private void OnRemove( XmlElement diffgramElement, XmlDiffPathNodeList matchNodes, 
                           XmlDiffViewParentNode sourceParent, ref XmlDiffViewNode currentPosition ) {
        // opid & descriptor
        XmlDiffViewOperation op = XmlDiffViewOperation.Remove;
        int opid = 0;
        OperationDescriptor opDesc = null;

        string opidAttr = diffgramElement.GetAttribute( "opid" );
        if ( opidAttr != string.Empty ) {
            opid = int.Parse( opidAttr );
            opDesc = GetDescriptor( opid );
            if ( opDesc._type == OperationDescriptor.Type.Move )
                op = XmlDiffViewOperation.MoveFrom;
        }
        
        // subtree
        string subtreeAttr = diffgramElement.GetAttribute( "subtree" );
        bool bSubtree = ( subtreeAttr != "no" );
        if ( !bSubtree ) {
            if ( matchNodes.Count != 1 )
                throw new Exception("The 'match' attribute of 'remove' element must select a single node when the 'subtree' attribute is specified." );
            // annotate node
            matchNodes.MoveNext();
            XmlDiffViewNode node = matchNodes.Current;
            AnnotateNode( node, op, opid, false );
            if ( opid != 0 )
                opDesc._nodeList.AddNode( node );

            // recurse
            ApplyDiffgram( diffgramElement, (XmlDiffViewParentNode)node );
        }
        else {
            // annotate nodes
            matchNodes.Reset();
            while ( matchNodes.MoveNext() ) {
                if ( opid != 0 )
                    opDesc._nodeList.AddNode( matchNodes.Current );
                AnnotateNode( matchNodes.Current, op, opid, true );
            }
        }
    }

    private void OnAddMatch( XmlElement diffgramElement, XmlDiffPathNodeList matchNodes, XmlDiffViewParentNode sourceParent, 
                            ref XmlDiffViewNode currentPosition ) {
        string opidAttr = diffgramElement.GetAttribute( "opid" );
        if ( opidAttr == string.Empty )
            throw new Exception( "Missing opid attribute." );

        // opid & descriptor
        int opid = int.Parse( opidAttr );
        OperationDescriptor opDesc = GetDescriptor( opid );

        string subtreeAttr = diffgramElement.GetAttribute( "subtree" );
        bool bSubtree = ( subtreeAttr != "no" );
        // move single node without subtree
        if ( !bSubtree ) {
            if ( matchNodes.Count != 1 )
                throw new Exception("The 'match' attribute of 'add' element must select a single node when the 'subtree' attribute is specified." );
            
            // clone node
            matchNodes.MoveNext();
            XmlDiffViewNode newNode = matchNodes.Current.Clone( false );
            AnnotateNode( newNode, XmlDiffViewOperation.MoveTo, opid, true );
            
            opDesc._nodeList.AddNode( newNode );

            // insert in tree
            sourceParent.InsertChildAfter( newNode, currentPosition, false );
            currentPosition = newNode;

            // recurse
            ApplyDiffgram( diffgramElement, (XmlDiffViewParentNode)newNode );
        }
        // move subtree
        else {
            matchNodes.Reset();
            while ( matchNodes.MoveNext() ) {
                XmlDiffViewNode newNode = matchNodes.Current.Clone( true );
                AnnotateNode( newNode, XmlDiffViewOperation.MoveTo, opid, true );

                opDesc._nodeList.AddNode( newNode );

                sourceParent.InsertChildAfter( newNode, currentPosition, false );
                currentPosition = newNode;
            }
        }
    }

    private void OnAddNode( XmlElement diffgramElement, string nodeTypeAttr, XmlDiffViewParentNode sourceParent, 
                            ref XmlDiffViewNode currentPosition ) {

        XmlNodeType nodeType = (XmlNodeType) int.Parse( nodeTypeAttr );
        string name = diffgramElement.GetAttribute( "name" );
        string prefix = diffgramElement.GetAttribute( "prefix" );
        string ns = diffgramElement.GetAttribute( "ns" );
        string opidAttr = diffgramElement.GetAttribute( "opid" );
        int opid = ( opidAttr == string.Empty ) ? 0 : int.Parse( opidAttr );
        
        if ( nodeType == XmlNodeType.Attribute ) {
            Debug.Assert( name != string.Empty );
            XmlDiffViewAttribute newAttr = new XmlDiffViewAttribute( name, prefix, ns, diffgramElement.InnerText );
            newAttr._op = XmlDiffViewOperation.Add;
            newAttr._opid = opid;
            ((XmlDiffViewElement)sourceParent).InsertAttributeAfter( newAttr, null );
        }
        else {
            XmlDiffViewNode newNode = null;
        
            switch ( nodeType ) {
			    case XmlNodeType.Element:
				    Debug.Assert( name != string.Empty );
				    newNode = new XmlDiffViewElement( name, prefix, ns, _bIgnorePrefixes );
				    ApplyDiffgram( diffgramElement, (XmlDiffViewParentNode)newNode );
				    break;
			    case XmlNodeType.Text:
			    case XmlNodeType.CDATA:
			    case XmlNodeType.Comment:
				    Debug.Assert( diffgramElement.InnerText != string.Empty );
				    newNode = new XmlDiffViewCharData( diffgramElement.InnerText, nodeType );
				    break;
			    case XmlNodeType.ProcessingInstruction:
				    Debug.Assert( diffgramElement.InnerText != string.Empty );
				    Debug.Assert( name != string.Empty );
				    newNode = new XmlDiffViewPI( name, diffgramElement.InnerText );
				    break;
			    case XmlNodeType.EntityReference:
				    Debug.Assert( name != string.Empty );
				    newNode = new XmlDiffViewER( name );
				    break;
                case XmlNodeType.XmlDeclaration:
                    Debug.Assert( diffgramElement.InnerText != string.Empty );
                    newNode = new XmlDiffViewXmlDeclaration( diffgramElement.InnerText );
                    break;
                case XmlNodeType.DocumentType:
                    newNode = new XmlDiffViewDocumentType( diffgramElement.GetAttribute( "name" ),
                                                           diffgramElement.GetAttribute( "publicId" ),
                                                           diffgramElement.GetAttribute( "systemId" ),
                                                           diffgramElement.InnerText );
                    break;
			    default:
				    Debug.Assert( false, "Invalid node type." ); 
				    break;
            }
            Debug.Assert( newNode != null );
            newNode._op = XmlDiffViewOperation.Add;
            newNode._opid = opid;
            sourceParent.InsertChildAfter( newNode, currentPosition, false );
            currentPosition = newNode;
        }
    }

    private void OnAddFragment( XmlElement diffgramElement, XmlDiffViewParentNode sourceParent, ref XmlDiffViewNode currentPosition ) {
        IEnumerator childNodes = diffgramElement.ChildNodes.GetEnumerator();
        while ( childNodes.MoveNext() ) {
            XmlDiffViewNode newChildNode = ImportNode( (XmlNode)childNodes.Current );
            sourceParent.InsertChildAfter( newChildNode, currentPosition, false );
            currentPosition = newChildNode;

            AnnotateNode( newChildNode, XmlDiffViewOperation.Add, 0, true );
        }
    }

    private XmlDiffViewNode ImportNode( XmlNode node ) {
        XmlDiffViewNode newNode = null;
        switch ( node.NodeType ) {
			case XmlNodeType.Element:
                XmlElement el = (XmlElement)node;
				XmlDiffViewElement newElement = new XmlDiffViewElement( el.LocalName, el.Prefix, el.NamespaceURI, _bIgnorePrefixes );
                // attributes
                IEnumerator attributes = node.Attributes.GetEnumerator();
                XmlDiffViewAttribute lastNewAttr  = null;
                while ( attributes.MoveNext() ) {
                    XmlAttribute at = (XmlAttribute)attributes.Current;
                    XmlDiffViewAttribute newAttr = new XmlDiffViewAttribute( at.LocalName, at.Prefix, at.NamespaceURI, at.Value );
                    newElement.InsertAttributeAfter( newAttr, lastNewAttr );
                    lastNewAttr = newAttr;
                }
                // children
                IEnumerator childNodes = node.ChildNodes.GetEnumerator();
                XmlDiffViewNode lastNewChildNode = null;
                while ( childNodes.MoveNext() ) {
                    XmlDiffViewNode newChildNode = ImportNode( (XmlNode)childNodes.Current );
                    newElement.InsertChildAfter( newChildNode, lastNewChildNode, false );
                    lastNewChildNode = newChildNode;
                }
                newNode = newElement;
				break;
			case XmlNodeType.Text:
			case XmlNodeType.CDATA:
			case XmlNodeType.Comment:
				newNode = new XmlDiffViewCharData( node.Value, node.NodeType );
				break;
			case XmlNodeType.ProcessingInstruction:
				newNode = new XmlDiffViewPI( node.Name, node.Value );
				break;
			case XmlNodeType.EntityReference:
				newNode = new XmlDiffViewER( node.Name );
				break;
            default:
                Debug.Assert( false, "Invalid node type." );
                break;
        }
        Debug.Assert( newNode != null );
        return newNode;
    }

    private void OnChange( XmlElement diffgramElement, XmlDiffPathNodeList matchNodes, 
                           XmlDiffViewParentNode sourceParent, ref XmlDiffViewNode currentPosition ) {
        Debug.Assert( matchNodes.Count == 1 );
        matchNodes.Reset();
        matchNodes.MoveNext();
        XmlDiffViewNode node = matchNodes.Current;

        if ( node._nodeType != XmlNodeType.Attribute )
            currentPosition = node;

        XmlDiffViewNode.ChangeInfo changeInfo = new XmlDiffViewNode.ChangeInfo();
        string name = diffgramElement.HasAttribute("name") ? diffgramElement.GetAttribute( "name" ) : null;
        string prefix = diffgramElement.HasAttribute("prefix") ? diffgramElement.GetAttribute( "prefix" ) : null;
        string ns = diffgramElement.HasAttribute("ns") ? diffgramElement.GetAttribute( "ns" ) : null;
        
        switch ( node._nodeType ) {
			case XmlNodeType.Element:
				changeInfo._localName = ( name == null )? ((XmlDiffViewElement)node)._localName : name;
                changeInfo._prefix = ( prefix == null ) ? ((XmlDiffViewElement)node)._prefix : prefix;
                changeInfo._ns = ( ns == null ) ? ((XmlDiffViewElement)node)._ns : ns;
				break;
            case XmlNodeType.Attribute:
                string value = diffgramElement.InnerText;
                if ( name == string.Empty && prefix == string.Empty && value == string.Empty ) 
                    return;
				changeInfo._localName = ( name == null ) ? ((XmlDiffViewAttribute)node)._localName : name;
                changeInfo._prefix = ( prefix == null ) ? ((XmlDiffViewAttribute)node)._prefix      : prefix;
                changeInfo._ns = ( ns == null ) ? ((XmlDiffViewAttribute)node)._ns : ns;
                changeInfo._value = diffgramElement.InnerText;
				break;
            case XmlNodeType.Text:
			case XmlNodeType.CDATA:
				Debug.Assert( diffgramElement.FirstChild != null );
				changeInfo._value = diffgramElement.InnerText;
				break;
			case XmlNodeType.Comment:
                Debug.Assert( diffgramElement.FirstChild != null );
                Debug.Assert( diffgramElement.FirstChild.NodeType == XmlNodeType.Comment );
                changeInfo._value = diffgramElement.FirstChild.Value;
                break;
			case XmlNodeType.ProcessingInstruction:
                if ( name == null ) {
                    Debug.Assert( diffgramElement.FirstChild != null );
                    Debug.Assert( diffgramElement.FirstChild.NodeType == XmlNodeType.ProcessingInstruction );
                    changeInfo._localName = diffgramElement.FirstChild.Name;
                    changeInfo._value = diffgramElement.FirstChild.Value;
                }
                else {
				    changeInfo._localName = name;
				    changeInfo._value = ((XmlDiffViewPI)node)._value;
                }
				break;
			case XmlNodeType.EntityReference:
				Debug.Assert( name != null );
				changeInfo._localName = name;
				break;
            case XmlNodeType.XmlDeclaration:
				Debug.Assert( diffgramElement.FirstChild != null );
				changeInfo._value = diffgramElement.InnerText;
                break;
            case XmlNodeType.DocumentType:
                changeInfo._localName = ( name == null ) ? ((XmlDiffViewDocumentType)node)._name : name ;
                
                if ( diffgramElement.HasAttribute( "publicId" ) )
                    changeInfo._prefix = diffgramElement.GetAttribute( "publicId" );
                else
                    changeInfo._prefix = ((XmlDiffViewDocumentType)node)._publicId;
                
                if ( diffgramElement.HasAttribute( "systemId" ) )
                    changeInfo._ns = diffgramElement.GetAttribute( "systemId" );
                else
                    changeInfo._ns = ((XmlDiffViewDocumentType)node)._systemId;

                if ( diffgramElement.FirstChild != null )
                    changeInfo._value = diffgramElement.InnerText;
                else
                    changeInfo._value = ((XmlDiffViewDocumentType)node)._subset;
                break;
			default:
				Debug.Assert( false, "Invalid node type." ); 
				break;
        }
        node._changeInfo = changeInfo;
        node._op = XmlDiffViewOperation.Change;

        string opidAttr = diffgramElement.GetAttribute( "opid" );
        if ( opidAttr != string.Empty ) {
            node._opid = int.Parse( opidAttr );
        }

        if ( node._nodeType == XmlNodeType.Element  &&
             diffgramElement.FirstChild != null ) {
            ApplyDiffgram( diffgramElement, (XmlDiffViewParentNode)node );
        }
    }

    private OperationDescriptor GetDescriptor( int opid ) {
        OperationDescriptor opDesc = (OperationDescriptor)_descriptors[opid];
        if ( opDesc == null )
            throw new Exception( "Invalid operation id." );
        return opDesc;
    }

    private void AnnotateNode( XmlDiffViewNode node, XmlDiffViewOperation op, int opid, bool bSubtree ) {
        node._op = op;
        node._opid =  opid;

        if ( node._nodeType == XmlNodeType.Element ) {
            XmlDiffViewAttribute attr = ((XmlDiffViewElement)node)._attributes;
            while ( attr != null ) {
                attr._op = op;
                attr._opid = opid;
                attr = (XmlDiffViewAttribute)attr._nextSibbling;
            }
        }

        if ( bSubtree ) {
            XmlDiffViewNode childNode = node.FirstChildNode;
            while ( childNode != null ) {
                AnnotateNode( childNode, op, opid, true );
                childNode = childNode._nextSibbling;
            }
        }
    }

    public void GetHtml( TextWriter htmlOutput ) {
        _doc.DrawHtml( new XmlTextWriter( htmlOutput ), 10 );
    }

    // Static methods and data for drawing
    static internal readonly string[] HtmlBgColor = {
            "background-color: white",  // Match    = 0,
            "background-color: white",  // Ignore   = 1,
            "background-color: yellow", // Add      = 2,
            "background-color: yellow", // MoveTo   = 3,
            "background-color: red",    // Remove   = 4,
            "background-color: red",    // MoveFrom = 5,
            "background-color: lightgreen", // Change   = 6,
    };

    static internal readonly string[] HtmlFgColor = {
            "black",  // Match    = 0,
            "#AAAAAA",// Ignore   = 1,
            "black",  // Add      = 2,
            "blue",   // MoveTo   = 3,
            "black",  // Remove   = 4,
            "blue",   // MoveFrom = 5,
            "black",  // Change   = 6,
    };

    static internal readonly bool[,] HtmlWriteToPane = {
                {  true,  true },  // Match    = 0,
                {  true,  true },  // Ignore   = 1,
                { false,  true },  // Add      = 2,
                { false,  true },  // MoveTo   = 3,
                {  true, false },  // Remove   = 4,
                {  true, false },  // MoveFrom = 5,
                {  true,  true },  // Change   = 6,
    };

    static internal readonly int DeltaIndent = 15;

    static private readonly string Nbsp = "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;" +
                                           "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;" +
                                           "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;";

    static private void HtmlSetColor( XmlWriter pane, XmlDiffViewOperation op ) {
        pane.WriteStartElement( "font" );
        pane.WriteAttributeString( "style", HtmlBgColor[(int)op] );
        pane.WriteAttributeString( "color", HtmlFgColor[(int)op] );
    }

    static private void HtmlResetColor( XmlWriter pane ) {
        pane.WriteFullEndElement();
    }

    static internal void HtmlWriteString( XmlWriter pane, string str ) {
        pane.WriteString( str );
    }

    static internal void HtmlWriteString( XmlWriter pane, XmlDiffViewOperation op, string str ) {
        HtmlSetColor( pane, op );
        pane.WriteString( str );
        HtmlResetColor( pane );
    }

    static internal void HtmlWriteEmptyString( XmlWriter pane ) {
        //pane.WriteCharEntity( (char)255 );
        pane.WriteRaw( "&nbsp;" );
    }

    static internal void HtmlStartCell( XmlWriter writer, int indent ) {
        writer.WriteStartElement( "td" );
        writer.WriteAttributeString( "style", "padding-left: " + indent.ToString() + "pt;" );
    }

    static internal void HtmlEndCell( XmlWriter writer ) {
        writer.WriteFullEndElement();
    }

    static internal void HtmlBr( XmlWriter writer ) {
        writer.WriteStartElement( "br" );
        writer.WriteEndElement();
    }

    static internal void HtmlStartRow( XmlWriter writer ) {
        writer.WriteStartElement( "tr" );
    }

    static internal void HtmlEndRow( XmlWriter writer ) {
        writer.WriteFullEndElement();
    }

    static internal string GetIndent( int charCount ) {
        int nbspCount = charCount * 6;
        if ( nbspCount <= Nbsp.Length ) {
            return Nbsp.Substring( 0, nbspCount );
        }
        else {
            string indent = string.Empty;
            while ( nbspCount > Nbsp.Length ) {
                indent += Nbsp;
                nbspCount -= Nbsp.Length;
            }
            indent += Nbsp.Substring( 0, nbspCount );
            return indent;
        }
    }

    internal static string NormalizeText( string text )
    {
        char[] chars = text.ToCharArray();
        int i = 0;
        int j = 0;

        for (;;)
        {
            while ( j < chars.Length  &&  IsWhitespace( text[j] ) )
                j++;

            while ( j < chars.Length && !IsWhitespace( text[j] ) )
                chars[i++]=chars[j++];

            if ( j < chars.Length )
            {
                chars[i++]=' ';
                j++;
            }
            else
            {
                if ( j == 0 )
                    return string.Empty;

                if ( IsWhitespace( chars[j-1] ) )
                    i--;

                return new string( chars, 0, i );
            }
        }
    }

    internal static bool IsWhitespace( char c )
    {
        return ( c == ' ' ||
                 c == '\t' ||
                 c == '\n' ||
                 c == '\r' );
    }
}
}
