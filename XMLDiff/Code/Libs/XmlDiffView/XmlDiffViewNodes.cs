//------------------------------------------------------------------------------
// <copyright file="XmlDiffViewNodes.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace Microsoft.XmlDiffPatch {

internal abstract class XmlDiffViewNode {
    
    internal class ChangeInfo {
        internal string _localName;
        internal string _prefix;    // publicId if DocumentType node
        internal string _ns;        // systemId if DocumentType node
        internal string _value;     // internal subset if DocumentType node
    }

    // node type
    internal XmlNodeType _nodeType;

    // tree pointers
    internal XmlDiffViewNode _nextSibbling = null;
    internal XmlDiffViewNode _parent = null;

    // diff operation
    internal XmlDiffViewOperation _op = XmlDiffViewOperation.Match;
    internal int _opid = 0; // operation id
    
    // a place to store change information (only if _op == XmlDiffViewOperation.Change)
    internal ChangeInfo _changeInfo = null;

	internal XmlDiffViewNode( XmlNodeType nodeType ) {
        _nodeType = nodeType;
	}

    internal abstract string OuterXml { get; }
    internal virtual XmlDiffViewNode FirstChildNode { get { return null; } }
    internal abstract XmlDiffViewNode Clone( bool bDeep );
    internal abstract void DrawHtml( XmlWriter writer, int indent );

    internal void DrawHtmlNoChange( XmlWriter writer, int indent ) {
        Debug.Assert( _nodeType != XmlNodeType.Element && _nodeType != XmlNodeType.Attribute );
        Debug.Assert( _op != XmlDiffViewOperation.Change );
        XmlDiffView.HtmlStartRow( writer );
        for ( int i = 0; i < 2; i++ ) {
            XmlDiffView.HtmlStartCell( writer, indent );
            if ( XmlDiffView.HtmlWriteToPane[(int)_op,i] ) {
                bool bCloseElement = OutputNavigation( writer );
                XmlDiffView.HtmlWriteString( writer, _op, OuterXml );
                if ( bCloseElement ) {
                    writer.WriteEndElement();
                }
            }
            else {
                XmlDiffView.HtmlWriteEmptyString( writer );
            }
            XmlDiffView.HtmlEndCell( writer );
        }
        XmlDiffView.HtmlEndRow( writer );
    }

    protected bool OutputNavigation( XmlWriter writer ) {
        if ( _parent == null || _parent._op != _op ) {
            switch ( _op ) {
                case XmlDiffViewOperation.MoveFrom:
                    writer.WriteStartElement( "a" );
                    writer.WriteAttributeString( "name", "move_from_" + _opid );
                    writer.WriteEndElement();
                    writer.WriteStartElement( "a" );
                    writer.WriteAttributeString( "href", "#move_to_" + _opid );
                    return true;
                case XmlDiffViewOperation.MoveTo:
                    writer.WriteStartElement( "a" );
                    writer.WriteAttributeString( "name", "move_to_" + _opid );
                    writer.WriteEndElement();
                    writer.WriteStartElement( "a" );
                    writer.WriteAttributeString( "href", "#move_from_" + _opid );
                    return true;
            }
        }
        return false;
    }
}

internal abstract class XmlDiffViewParentNode : XmlDiffViewNode
{
    // child nodes
    internal XmlDiffViewNode _childNodes;
    // number of source child nodes
    internal int _sourceChildNodesCount;
    // source nodes indexed by their relative position
    XmlDiffViewNode[] _sourceChildNodesIndex;

    internal XmlDiffViewParentNode( XmlNodeType nodeType ) : base( nodeType ) {}

    internal override XmlDiffViewNode FirstChildNode{ get { return _childNodes; } }

    internal XmlDiffViewNode GetSourceChildNode( int index ) { 
        if (index < 0 || index >= _sourceChildNodesCount || _sourceChildNodesCount == 0)
            throw new ArgumentException( "index" );

        if ( _sourceChildNodesCount == 0 )
            return null;

        if ( _sourceChildNodesIndex == null )
            CreateSourceNodesIndex();

        return _sourceChildNodesIndex[index];
    }

    internal void CreateSourceNodesIndex()
    {
        if (_sourceChildNodesIndex != null || _sourceChildNodesCount == 0)
            return;

        _sourceChildNodesIndex = new XmlDiffViewNode[_sourceChildNodesCount];
        
        XmlDiffViewNode curChild = _childNodes;
        for ( int i = 0; i < _sourceChildNodesCount; i++, curChild = curChild._nextSibbling ) {
            Debug.Assert( curChild != null );
            _sourceChildNodesIndex[i] = curChild;
        }
        Debug.Assert( curChild == null );
    }

    internal void InsertChildAfter( XmlDiffViewNode newChild, XmlDiffViewNode referenceChild, bool bSourceNode ) {
        Debug.Assert( newChild != null );
        if ( referenceChild == null ) {
            newChild._nextSibbling = _childNodes;
            _childNodes = newChild;
        }
        else {
            newChild._nextSibbling = referenceChild._nextSibbling;
            referenceChild._nextSibbling = newChild;
        }
        if ( bSourceNode )
            _sourceChildNodesCount++;
        newChild._parent = this;
    }

    internal void HtmlDrawChildNodes( XmlWriter writer, int indent ) {
        XmlDiffViewNode curChild = _childNodes;
        while ( curChild != null ) {
            curChild.DrawHtml( writer, indent );
            curChild = curChild._nextSibbling;
        }
    }
}

internal class XmlDiffViewDocument : XmlDiffViewParentNode
{
    internal XmlDiffViewDocument() : base( XmlNodeType.Document ) {
    }

    internal override string OuterXml { 
        get { 
            throw new Exception( "OuterXml is not supported on XmlDiffViewElement." );
        }
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        throw new Exception( "Clone method should never be called on a document node." );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        HtmlDrawChildNodes( writer, indent );
    }
}

internal class XmlDiffViewElement : XmlDiffViewParentNode
{
    // name
    internal string _localName;
    internal string _prefix;
    internal string _ns;
    internal string _name;

    // attributes
    internal XmlDiffViewAttribute _attributes;

    bool _ignorePrefixes;

    internal XmlDiffViewElement( string localName, string prefix, string ns, bool ignorePrefixes ) : base( XmlNodeType.Element ) {
        _localName = localName;
        _prefix = prefix;
        _ns = ns;
        
        if ( _prefix != string.Empty )
            _name = _prefix + ":" + _localName;
        else
            _name = _localName;

        _ignorePrefixes = ignorePrefixes;
    }

    internal XmlDiffViewAttribute GetAttribute( string name ) {
        XmlDiffViewAttribute curAttr = _attributes;
        while ( curAttr != null ) {
            if ( curAttr._name == name && curAttr._op == XmlDiffViewOperation.Match )
                return curAttr;
            curAttr = (XmlDiffViewAttribute)curAttr._nextSibbling;
        }
        return null;
    }

    internal void InsertAttributeAfter( XmlDiffViewAttribute newAttr, XmlDiffViewAttribute refAttr ) {
        Debug.Assert( newAttr != null );
        if ( refAttr == null ) {
            newAttr._nextSibbling = _attributes;
            _attributes = newAttr;
        }
        else {
            newAttr._nextSibbling = refAttr._nextSibbling;
            refAttr._nextSibbling = newAttr;
        }
        newAttr._parent = this;
    }

    internal override string OuterXml { 
        get { 
            throw new Exception( "OuterXml is not supported on XmlDiffViewElement." );
        }
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        XmlDiffViewElement newElement = new XmlDiffViewElement( _localName, _prefix, _ns, _ignorePrefixes );

        // attributes
        {
            XmlDiffViewAttribute curAttr = _attributes;
            XmlDiffViewAttribute lastNewAtt = null;
            while ( curAttr != null ) {
                XmlDiffViewAttribute newAttr = (XmlDiffViewAttribute)curAttr.Clone( true ); 
                newElement.InsertAttributeAfter( newAttr, lastNewAtt );
                lastNewAtt = newAttr;
                
                curAttr = (XmlDiffViewAttribute)curAttr._nextSibbling;
            }
        }

        if ( !bDeep ) 
            return newElement;

        // child nodes
        {
            XmlDiffViewNode curChild = _childNodes;
            XmlDiffViewNode lastNewChild = null;
            while ( curChild != null ) {
                XmlDiffViewNode newChild = curChild.Clone( true );
                newElement.InsertChildAfter( newChild, lastNewChild, false );
                lastNewChild = newChild;
                
                curChild = curChild._nextSibbling;
            }
        }

        return newElement;
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        XmlDiffViewOperation opForColor = _op;
        bool bCloseElement = false;
        XmlDiffView.HtmlStartRow( writer );
        for ( int i = 0; i < 2; i++ ) {
            XmlDiffView.HtmlStartCell( writer, indent );
            if ( XmlDiffView.HtmlWriteToPane[(int)_op,i] ) {
                bCloseElement = OutputNavigation( writer );

                if ( _op == XmlDiffViewOperation.Change ) {
                    opForColor = XmlDiffViewOperation.Match;
                    XmlDiffView.HtmlWriteString( writer, opForColor, "<" );
                    if ( i == 0 ) 
                        DrawHtmlNameChange( writer, _localName, _prefix );
                    else 
                        DrawHtmlNameChange( writer, _changeInfo._localName, _changeInfo._prefix );
                }
                else { 
                    DrawHtmlName( writer, opForColor, "<", string.Empty );
                }

                if ( bCloseElement ) {
                    writer.WriteEndElement();
                    bCloseElement = false;
                }

                // attributes
                DrawHtmlAttributes( writer, i );

                // close start tag
                if ( _childNodes != null ) 
                    XmlDiffView.HtmlWriteString( writer, opForColor, ">" );
                else
                    XmlDiffView.HtmlWriteString( writer, opForColor, "/>" );
            }
            else 
                XmlDiffView.HtmlWriteEmptyString( writer );
            XmlDiffView.HtmlEndCell( writer );
        }
        XmlDiffView.HtmlEndRow( writer );

        // child nodes
        if ( _childNodes != null ) {
            HtmlDrawChildNodes( writer, indent + XmlDiffView.DeltaIndent );

            // end element
            XmlDiffView.HtmlStartRow( writer );
            for ( int i = 0; i < 2; i++ ) {
                XmlDiffView.HtmlStartCell( writer, indent );
                if ( XmlDiffView.HtmlWriteToPane[(int)_op,i] ) {
                    if ( _op == XmlDiffViewOperation.Change ) {
                        Debug.Assert( opForColor == XmlDiffViewOperation.Match );
                        XmlDiffView.HtmlWriteString( writer, opForColor, "</" );
                        if ( i == 0 ) 
                            DrawHtmlNameChange( writer, _localName, _prefix );
                        else 
                            DrawHtmlNameChange( writer, _changeInfo._localName, _changeInfo._prefix );
                        XmlDiffView.HtmlWriteString( writer, opForColor, ">" );
                    }
                    else { 
                        DrawHtmlName( writer, opForColor, "</", ">" );
                    }
                }
                else 
                    XmlDiffView.HtmlWriteEmptyString( writer );
                XmlDiffView.HtmlEndCell( writer );
            }
            XmlDiffView.HtmlEndRow( writer );
        }
    }

    private void DrawHtmlAttributes( XmlWriter writer, int paneNo ) {
        if ( _attributes == null )
            return;

        string attrIndent = string.Empty;
        if ( _attributes._nextSibbling != null ) {
            attrIndent = XmlDiffView.GetIndent( _name.Length + 2 );
        }
        XmlDiffViewAttribute curAttr = _attributes;
        while ( curAttr != null ) {
            if ( XmlDiffView.HtmlWriteToPane[(int)curAttr._op, paneNo ] ) {
                if ( curAttr == _attributes ) 
                    writer.WriteString( " " );
                else 
                    writer.WriteRaw( attrIndent );
            
                if ( curAttr._op == XmlDiffViewOperation.Change ) {
                    if ( paneNo == 0 )
                        DrawHtmlAttributeChange( writer, curAttr, curAttr._localName, curAttr._prefix, curAttr._value );
                    else
                        DrawHtmlAttributeChange( writer, curAttr, curAttr._changeInfo._localName, curAttr._changeInfo._prefix, 
                                                curAttr._changeInfo._value );
                }
                else {
                    DrawHtmlAttribute( writer, curAttr, curAttr._op );
                }
            }
            else 
                XmlDiffView.HtmlWriteEmptyString( writer );

            curAttr = (XmlDiffViewAttribute)curAttr._nextSibbling;
            if ( curAttr != null ) 
                XmlDiffView.HtmlBr( writer );
        }    
    }

    private void DrawHtmlNameChange( XmlWriter writer, string localName, string prefix ) {
        if ( prefix != string.Empty ) {
            XmlDiffView.HtmlWriteString( writer, _ignorePrefixes ? XmlDiffViewOperation.Ignore : 
                                                                  (_prefix == _changeInfo._prefix) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change, 
                                         prefix + ":" );
        }

        XmlDiffView.HtmlWriteString( writer, 
                                     (_localName == _changeInfo._localName) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change,
                                     localName );
    }

    private void DrawHtmlName( XmlWriter writer, XmlDiffViewOperation opForColor, string tagStart, string tagEnd ) {
        if ( _prefix != string.Empty && _ignorePrefixes ) {
            XmlDiffView.HtmlWriteString( writer, opForColor, tagStart );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Ignore, _prefix + ":" );
            XmlDiffView.HtmlWriteString( writer, opForColor, _localName + tagEnd );
        }
        else {
            XmlDiffView.HtmlWriteString( writer, opForColor, tagStart + _name + tagEnd );
        }

        
    }

    private void DrawHtmlAttributeChange( XmlWriter writer, XmlDiffViewAttribute attr, string localName, string prefix, string value ) {
        if ( prefix != string.Empty ) {
            XmlDiffView.HtmlWriteString( writer, 
                                        _ignorePrefixes ? XmlDiffViewOperation.Ignore : 
                                                          (attr._prefix == attr._changeInfo._prefix) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change, 
                                        prefix + ":" );
        }

        XmlDiffView.HtmlWriteString( writer, 
                                    (attr._localName == attr._changeInfo._localName) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change,
                                    localName );

        if ( attr._value != attr._changeInfo._value ) {
            XmlDiffView.HtmlWriteString( writer, "=\"" );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, value );
            XmlDiffView.HtmlWriteString( writer, "\"" );
        }
        else {
            XmlDiffView.HtmlWriteString( writer, "=\"" + value + "\"" );
        }
    }

    private void DrawHtmlAttribute( XmlWriter writer, XmlDiffViewAttribute attr, XmlDiffViewOperation opForColor ) {
        if ( _ignorePrefixes ) {
            if ( attr._prefix == "xmlns" || ( attr._localName == "xmlns" && attr._prefix == string.Empty ) ) {
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Ignore, attr._name );
                XmlDiffView.HtmlWriteString( writer, opForColor , "=\"" + attr._value + "\"" );
                return;
            }
            else if ( attr._prefix != string.Empty ) {
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Ignore, attr._prefix + ":" );
                XmlDiffView.HtmlWriteString( writer, opForColor , attr._localName + "=\"" + attr._value + "\"" );
                return;
            }
        }

        XmlDiffView.HtmlWriteString( writer, opForColor , attr._name + "=\"" + attr._value + "\"" );
    }
}

internal class XmlDiffViewAttribute : XmlDiffViewNode
{
    // name
    internal string _localName;
    internal string _prefix;
    internal string _ns;
    internal string _name; // == _prefix + ":" + _localName;

    // value
    internal string _value;

    internal XmlDiffViewAttribute( string localName, string prefix, string ns, string name, string value ) : base( XmlNodeType.Attribute ) {
        _localName = localName;
        _prefix = prefix;
        _ns = ns;
        _value = value;
        _name = name;
    }

    internal XmlDiffViewAttribute( string localName, string prefix, string ns, string value ) : base( XmlNodeType.Attribute ) { 
        _localName = localName;
        _prefix = prefix;
        _ns = ns;
        _value = value;

        if ( prefix == string.Empty )
            _name = _localName;
        else
            _name = _prefix + ":" + _localName;
    }

    internal override string OuterXml {
        get { 
            string outerXml = string.Empty;
            if ( _prefix != string.Empty )
                outerXml = _prefix + ":";
            outerXml += _localName + "=\"" + _value + "\"";
            return outerXml;
        }
    }
    
    internal override XmlDiffViewNode Clone( bool bDeep ) {
        return new XmlDiffViewAttribute( _localName, _prefix, _ns, _name, _value );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        throw new Exception( "This methods should never be called." );
    }

}

internal class XmlDiffViewCharData : XmlDiffViewNode
{
    internal string _value;

    internal XmlDiffViewCharData( string value, XmlNodeType nodeType ) : base( nodeType ) {
        _value = value;
    }
    
    internal override string OuterXml {
        get { 
            switch ( _nodeType )
            {
                case XmlNodeType.Text: 
                case XmlNodeType.Whitespace: 
                    return _value; 
                case XmlNodeType.Comment:
                    return "<!--" + _value + "-->";
                case XmlNodeType.CDATA:
                    return "<!CDATA[" + _value + "]]>";
                default:
                    Debug.Assert( false, "Invalid node type." );
                    return string.Empty;
            }
        }
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        return new XmlDiffViewCharData( _value, _nodeType );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        if ( _op == XmlDiffViewOperation.Change ) {
            string openString = string.Empty;
            string closeString = string.Empty;
            if ( _nodeType == XmlNodeType.CDATA ) { 
                openString = "<!CDATA[";
                closeString = "]]>";
            }
            else if ( _nodeType == XmlNodeType.Comment ) { 
                openString = "<!--";
                closeString = "-->";
            }

            XmlDiffView.HtmlStartRow( writer );
            XmlDiffView.HtmlStartCell( writer, indent );
            if ( openString != string.Empty ) {
                XmlDiffView.HtmlWriteString( writer, openString );
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _value );
                XmlDiffView.HtmlWriteString( writer, closeString );
            }
            else
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _value );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlStartCell( writer, indent );
            
            if ( openString != string.Empty ) {
                XmlDiffView.HtmlWriteString( writer, openString );
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _changeInfo._value );
                XmlDiffView.HtmlWriteString( writer, closeString );
            }
            else
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _changeInfo._value );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlEndRow( writer );
        }
        else {
            DrawHtmlNoChange( writer, indent );
        }
    }
}

internal class XmlDiffViewPI : XmlDiffViewCharData
{
    internal string _name;

    internal XmlDiffViewPI( string name, string value ) : base( value, XmlNodeType.ProcessingInstruction ) {
        _name = name;
    }

    internal override string OuterXml {
        get { return "<?" + _name + " " + _value + "?>"; }
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        return new XmlDiffViewPI( _name, _value );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        if ( _op == XmlDiffViewOperation.Change ) {
            XmlDiffViewOperation nameOp = (_name == _changeInfo._localName) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change;
            XmlDiffViewOperation valueOp = (_value == _changeInfo._value) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change;

            XmlDiffView.HtmlStartRow( writer );
            XmlDiffView.HtmlStartCell( writer, indent );
 
            XmlDiffView.HtmlWriteString( writer, "<?" );
            XmlDiffView.HtmlWriteString( writer, nameOp, _name );
            XmlDiffView.HtmlWriteString( writer, " " );
            XmlDiffView.HtmlWriteString( writer, valueOp, _value );
            XmlDiffView.HtmlWriteString( writer, "?>" );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlStartCell( writer, indent );

            XmlDiffView.HtmlWriteString( writer, "<?" );
            XmlDiffView.HtmlWriteString( writer, nameOp, _changeInfo._localName );
            XmlDiffView.HtmlWriteString( writer, " " );
            XmlDiffView.HtmlWriteString( writer, valueOp, _changeInfo._value );
            XmlDiffView.HtmlWriteString( writer, "?>" );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlEndRow( writer );
        }     
        else {
            DrawHtmlNoChange( writer, indent );
        }
    }
}

internal class XmlDiffViewER : XmlDiffViewNode
{
    string _name;

    internal XmlDiffViewER( string name ) : base( XmlNodeType.EntityReference ) {
        _name = name;
    }

    internal override string OuterXml {
        get { return "&" + _name + ";"; } 
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        return new XmlDiffViewER( _name );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        if ( _op == XmlDiffViewOperation.Change ) {
            Debug.Assert( _name != _changeInfo._localName );

            XmlDiffView.HtmlStartRow( writer );
            XmlDiffView.HtmlStartCell( writer, indent );

            XmlDiffView.HtmlWriteString( writer, "&" );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _name );
            XmlDiffView.HtmlWriteString( writer, ";" );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlStartCell( writer, indent );

            XmlDiffView.HtmlWriteString( writer, "&" );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _changeInfo._localName );
            XmlDiffView.HtmlWriteString( writer, ";" );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlEndRow( writer );
        }
        else {
            DrawHtmlNoChange( writer, indent );
        }
    }
}

internal class XmlDiffViewDocumentType : XmlDiffViewNode
{
    internal string _name;
    internal string _systemId;
    internal string _publicId;
    internal string _subset;

    internal XmlDiffViewDocumentType( string name, string publicId, string systemId, string subset ) : base( XmlNodeType.DocumentType ) {
        _name = name;
        _publicId = ( publicId == null ) ? string.Empty : publicId;
        _systemId = ( systemId == null ) ? string.Empty : systemId;
        _subset = subset;
    }

    internal override string OuterXml {
        get { 
            string dtd = "<!DOCTYPE " + _name + " ";
            if ( _publicId != string.Empty ) {
                dtd += "PUBLIC \"" + _publicId + "\" ";
            }
            else if ( _systemId != string.Empty ) {
                dtd += "SYSTEM \"" + _systemId + "\" ";
            }

            if ( _subset != string.Empty )
                dtd += "[" + _subset + "]";

            dtd += ">";
            return dtd;
        } 
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        Debug.Assert( false, "Clone method should never be called on document type node." );
        return null;
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        if ( _op == XmlDiffViewOperation.Change ) {
            XmlDiffView.HtmlStartRow( writer );
            for ( int i = 0; i < 2; i++ ) {
                XmlDiffView.HtmlStartCell( writer, indent );
                // name
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, "<!DOCTYPE " );
                if ( i == 0 ) 
                    XmlDiffView.HtmlWriteString( writer, ( _name == _changeInfo._localName) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change, _name );
                else 
                    XmlDiffView.HtmlWriteString( writer, ( _name == _changeInfo._localName) ? XmlDiffViewOperation.Match : XmlDiffViewOperation.Change, _changeInfo._localName );
                
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, " " );

                string systemString = "SYSTEM ";
                // public id
                if ( _publicId == _changeInfo._prefix ) {
                    // match
                    if ( _publicId != string.Empty ) {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, "PUBLIC \"" + _publicId + "\" " );
                        systemString = string.Empty;
                    }
                }
                else {
                    // add
                    if ( _publicId == string.Empty ) {
                        if ( i == 1 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Add, "PUBLIC \"" + _changeInfo._prefix + "\" " );
                            systemString = string.Empty;
                        }
                    }
                    // remove
                    else if ( _changeInfo._prefix == string.Empty ) {
                        if ( i == 0 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Remove, "PUBLIC \"" + _publicId + "\" " );
                            systemString = string.Empty;
                        }
                    }
                    // change
                    else {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, "PUBLIC \"" + ( ( i == 0 ) ? _publicId : _changeInfo._prefix )+ "\" " );
                        systemString = string.Empty;
                    }
                }

                // system id
                if ( _systemId == _changeInfo._ns ) {
                    if (  _systemId != string.Empty ) {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, systemString  + "\"" + _systemId + "\" " );
                    }
                }
                else { 
                    // add 
                    if ( _systemId == string.Empty ) {
                        if ( i == 1 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Add, systemString  + "\"" + _changeInfo._ns + "\" " );
                        }                
                    }
                    // remove
                    else if ( _changeInfo._prefix == string.Empty ) {
                        if ( i == 0 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Remove, systemString  + "\"" +  _systemId + "\" " );
                        }
                    }
                    // change
                    else {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, systemString  + "\"" + ( ( i == 0 ) ? _systemId : _changeInfo._ns ) + "\" " );
                    }
                }

                // internal subset
                if ( _subset == _changeInfo._value ) {
                    if ( _subset != string.Empty ) {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, "[" + _subset + "]" );
                    }
                }
                else {
                    // add 
                    if ( _subset == string.Empty ) {
                        if ( i == 1 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Add, "[" + _changeInfo._value + "]" );
                        }                
                    }
                    // remove
                    else if ( _changeInfo._value == string.Empty ) {
                        if ( i == 0 ) {
                            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Remove, "[" + _subset + "]" );
                        }
                    }
                    // change
                    else {
                        XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, "[" + ( ( i == 0 ) ? _subset : _changeInfo._value ) + "]" );
                    }
                }

                // close start tag
                XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Match, ">" );
                XmlDiffView.HtmlEndCell( writer );
            }
            XmlDiffView.HtmlEndRow( writer );
        }
        else {
            DrawHtmlNoChange( writer, indent );
        }
    }
}

internal class XmlDiffViewXmlDeclaration : XmlDiffViewNode
{
    string _value;

    internal XmlDiffViewXmlDeclaration( string value ) : base( XmlNodeType.XmlDeclaration ) {
        _value = value;
    }

    internal override string OuterXml {
        get { return "<?xml " + _value + "?>"; } 
    }

    internal override XmlDiffViewNode Clone( bool bDeep ) {
        return new XmlDiffViewXmlDeclaration( _value );
    }

    internal override void DrawHtml( XmlWriter writer, int indent ) {
        if ( _op == XmlDiffViewOperation.Change ) {
            Debug.Assert( _value != _changeInfo._value );

            XmlDiffView.HtmlStartRow( writer );
            XmlDiffView.HtmlStartCell( writer, indent );
            XmlDiffView.HtmlWriteString( writer, "<?xml " );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _value );
            XmlDiffView.HtmlWriteString( writer, "?>" );
            
            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlStartCell( writer, indent );

            XmlDiffView.HtmlWriteString( writer, "<?xml " );
            XmlDiffView.HtmlWriteString( writer, XmlDiffViewOperation.Change, _changeInfo._value );
            XmlDiffView.HtmlWriteString( writer, "?>" );

            XmlDiffView.HtmlEndCell( writer );
            XmlDiffView.HtmlEndRow( writer );
        }
        else {
            DrawHtmlNoChange( writer, indent );
        }
    }
}
}