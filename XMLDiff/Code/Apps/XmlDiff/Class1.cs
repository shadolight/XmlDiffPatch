using System;
using System.Xml;
using System.Text;
using Microsoft.XmlDiffPatch;

namespace XmlDiffApp {
    class Class1 {
        static void Main(string[] args) {
            bool fragments = false;
            XmlDiffAlgorithm algorithm = XmlDiffAlgorithm.Auto;
            XmlDiffOptions options = XmlDiffOptions.None;

            // process options
            int curArgsIndex = 0;
            while ( curArgsIndex < args.Length && 
                ( args[curArgsIndex][0] == '/' || args[curArgsIndex][0] == '-' ) ) {

                if ( args[curArgsIndex].Length != 2 ) {
                    Console.WriteLine( "Invalid option: " + args[curArgsIndex] );
                    WriteUsage();
                    return;
                }
                
                switch ( args[curArgsIndex][1] ) {
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
                        fragments = true;
                        break;
                    case 't':
                        algorithm = XmlDiffAlgorithm.Fast;
                        break;
                    case 'z':
                        algorithm = XmlDiffAlgorithm.Precise;
                        break;
                    case '?':
                        WriteUsage();
                        return;
                    default:
                        Console.Write( "Invalid option: " + args[curArgsIndex] + "\n" );
                        return;
                }
                curArgsIndex++;
            }

            if ( args.Length < 2 ) {
                Console.WriteLine( "Invalid arguments." );
                WriteUsage();
                return;
            }

            // extract names from command line
            string sourceXmlFileName = args[ curArgsIndex ];
            string changedXmlFileName = args[ curArgsIndex + 1 ];
            string diffgramFileName = ( curArgsIndex + 2 < args.Length ) ? args[ curArgsIndex + 2 ] : null;

            Console.WriteLine( "Comparing " + sourceXmlFileName + " to " + changedXmlFileName  );

            // create XmlTextWriter where the diffgram will be saved
            XmlWriter diffgramWriter = null;
            if ( diffgramFileName != null ) {
                diffgramWriter = new XmlTextWriter( diffgramFileName, Encoding.Unicode );
            }

            // create XmlDiff object & set the desired options and algorithm
            XmlDiff xmlDiff = new XmlDiff( options );
            xmlDiff.Algorithm = algorithm;

            // Compare the XML files
            bool bEqual = false;
            try {
                bEqual = xmlDiff.Compare( sourceXmlFileName, changedXmlFileName, fragments, diffgramWriter );			
            }
            catch (Exception e) {
                WriteError(e.Message);
                return;
            }
            if (bEqual) {
                Console.WriteLine( "Files are identical." );
            }
            else {
                Console.WriteLine( "Files are different." ); 
            }
            if ( diffgramWriter != null ) {
                diffgramWriter.Close();
                Console.WriteLine( "XDL diffgram has been saved to " + diffgramFileName + "." );
            }
        }

        static private void WriteError(string errorMessage) {
            Console.WriteLine( "Error:" + errorMessage);
        }

        static private void WriteUsage() {
            Console.WriteLine( 
                "USAGE: xmldiff [options] <source_xml> <changed_xml> [<diffgram>]\n\n" +
                "source_xml    name of the file with the original base XML document or fragment\n" +
                "changed_xml   name of the file with the changed XML document or fragment\n" +
                "diffgram      name of the file with file where the XDL diffgram will be stored (optional)\n\n" +
                "Options:\n" +
                "/o    ignore child order\n" +
                "/c    ignore comments\n" + 
                "/p    ignore processing instructions\n" + 
                "/w    ignore whitespaces, normalize text value\n" + 
                "/n    ignore namespaces\n" +
                "/r    ignore prefixes\n" + 
                "/x    ignore XML declaration\n" + 
                "/d    ignore DTD\n" +
                "/f    the files contain XML fragments\n" +
                "/t    use XmlDiffAlgorithm.Fast (walk-tree algorithm)\n" +
                "/z    use XmlDiffAlgorithm.Precise (tree-distance Zhang-Shasha algorithm)\n" +
                "If no options specified, nothing above is ignored and the XmlDiff.exe will automatically determine the algorithm for you\n"
                );
        }
    }
}
