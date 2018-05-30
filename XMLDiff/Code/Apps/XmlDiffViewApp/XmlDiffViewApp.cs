//------------------------------------------------------------------------------
// <copyright file="XmlDiffViewApp.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.XmlDiffPatch;

namespace XmlDiffViewApp {

class XmlDiffViewApp {

    static void WriteUsage() {
        Console.WriteLine( "USAGE: XmlDiffView [options] sourceXmlFile changesXmlFile resultHtmlViewFile\n" +  
                                "Options:\n" +
                                "/?    show this help\n" +
                                "/o    ignore child order\n" +
                                "/c    ignore comments\n" + 
                                "/p    ignore processing instructions\n" + 
                                "/w    ignore whitespaces, normalize text value\n" + 
                                "/n    ignore namespaces\n" +
                                "/r    ignore prefixes\n" + 
                                "/x    ignore XML declaration\n" + 
                                "/d    ignore DTD\n" +
                                "/f    fragments\n" );
    }

   
    static void Main( string[] args ) {
        try {

            int curArgIndex = 0;
            bool bFragment = false;

            // decode options
            XmlDiffOptions options = XmlDiffOptions.None;
            string optionsString = string.Empty;
            while ( curArgIndex < args.Length  && 
                    ( args[curArgIndex][0] == '/' || args[curArgIndex][0] == '-' ) ) {
                if ( args[curArgIndex].Length != 2 ) {
                    System.Console.Write( "Invalid option: " + args[curArgIndex] + "\n" );
                    return;
                }
                
                switch ( args[curArgIndex][1] ) {
                    case '?': 
                        WriteUsage();
                        return;
                    case 'o': 
                        options |= XmlDiffOptions.IgnoreChildOrder;
                        break;
                    case 'c':
                        options |= XmlDiffOptions.IgnoreComments;
                        break;
                    case 'p':
                        options |= XmlDiffOptions.IgnorePI;
                        break;
                    case 'w':
                        options |= XmlDiffOptions.IgnoreWhitespace;
                        break;
                    case 'n':
                        options |= XmlDiffOptions.IgnoreNamespaces;
                        break;
                    case 'r':
                        options |= XmlDiffOptions.IgnorePrefixes;
                        break;
                    case 'x':
                        options |= XmlDiffOptions.IgnoreXmlDecl;
                        break;
                    case 'd':
                        options |= XmlDiffOptions.IgnoreDtd;
                        break;
                    case 'f':
                        bFragment = true;
                        break;
                    default:
                        System.Console.Write( "Invalid option: " + args[curArgIndex] + "\n" );
                        return;
                }
                optionsString += args[curArgIndex][1];
                curArgIndex++;
            }

            if ( args.Length - curArgIndex < 3 ) {
                WriteUsage();
                return;
            }

            string sourceXmlFile = args[curArgIndex];
            string changedXmlFile = args[curArgIndex+1];
            string resultHtmlViewFile = args[curArgIndex+2];

            MemoryStream diffgram = new MemoryStream();
            XmlTextWriter diffgramWriter = new XmlTextWriter( new StreamWriter( diffgram ) );

            Console.WriteLine( "Comparing " + sourceXmlFile + " & " + changedXmlFile );
            XmlDiff xmlDiff = new XmlDiff( options );
            bool bIdentical = xmlDiff.Compare( sourceXmlFile, changedXmlFile, bFragment, diffgramWriter );

            Console.WriteLine( "Files compared " + (bIdentical ? "identical." : "different.") );

            TextWriter resultHtml = new StreamWriter( new FileStream( resultHtmlViewFile, FileMode.Create, FileAccess.Write ));
            resultHtml.WriteLine( "<html><head>");
            resultHtml.WriteLine( "<style TYPE='text/css' MEDIA='screen'>");
            resultHtml.Write( "<!-- td { font-family: Courier New; font-size:14; } " + 
                                "th { font-family: Arial; } " +
                                "p { font-family: Arial; } -->" );
            resultHtml.WriteLine( "</style></head>" );
            resultHtml.WriteLine( "<body><h3 style='font-family:Arial'>XmlDiff view</h3><table border='0'><tr><td><table border='0'>" );
            resultHtml.WriteLine( "<tr><th>" + sourceXmlFile + "</th><th>" + 
                                            changedXmlFile + "</th></tr>" +
                                "<tr><td colspan=2><hr size=1></td></tr>" );
            if ( bIdentical ) {
                resultHtml.WriteLine( "<tr><td colspan='2' align='middle'>Files are identical.</td></tr>" );
            }
            else {
                resultHtml.WriteLine( "<tr><td colspan='2' align='middle'>Files are different.</td></tr>" );
            }

            diffgram.Seek( 0,SeekOrigin.Begin );
            XmlDiffView xmlDiffView = new Microsoft.XmlDiffPatch.XmlDiffView();
            XmlTextReader sourceReader;
            if ( bFragment ) {
                    NameTable nt = new NameTable();
                    sourceReader = new XmlTextReader( new FileStream( sourceXmlFile, FileMode.Open, FileAccess.Read ),
                                                      XmlNodeType.Element,
                                                      new XmlParserContext( nt, new XmlNamespaceManager( nt ),
                                                                            string.Empty, XmlSpace.Default ) );
            }
            else {
                sourceReader = new XmlTextReader( sourceXmlFile );
            }

            sourceReader.XmlResolver = null;
            xmlDiffView.Load( sourceReader, new XmlTextReader( diffgram ) );

            xmlDiffView.GetHtml( resultHtml );

            resultHtml.WriteLine( "</table></table></body></html>" );
            resultHtml.Close();

            Console.WriteLine( resultHtmlViewFile + " saved successfully." );
        }
        catch ( Exception e ) {
            Console.WriteLine( "Error: " + e.Message );
        }

        if ( System.Diagnostics.Debugger.IsAttached ) 
        {
            Console.Write( "\nPress enter...\n" );
            Console.Read();
        }

    }
}
 
}
