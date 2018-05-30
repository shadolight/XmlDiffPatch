using System;
using System.IO;
using System.Xml;
using System.Text;
using Microsoft.XmlDiffPatch;

namespace XmlPatchApp {
    class Class1 {
        static void Main(string[] args) {
            if ( args.Length < 3 ) {
                Console.WriteLine( "Invalid arguments." );
                WriteUsage();
                return;
            }

            // extract names from command line
            string sourceXmlFileName = args[0];
            string diffgramFileName = args[1];
            string patchedXmlFileName = args[2];

            Console.WriteLine( "Patching " + sourceXmlFileName + " with " + diffgramFileName + "\n");

            FileStream patchedFile = new FileStream( patchedXmlFileName, FileMode.Create, FileAccess.Write );

            XmlPatch xmlPatch = new XmlPatch();
            try {
                xmlPatch.Patch( sourceXmlFileName, patchedFile, new XmlTextReader( diffgramFileName ) );
            }
            catch (Exception ex) {
                WriteError(ex.Message);
                return;
            }

            patchedFile.Close();

            Console.WriteLine( "The patched document or fragment has been saved to " + patchedXmlFileName );
        }
        
        static private void WriteError(string errorMessage) {
            Console.WriteLine("Error: " + errorMessage + "\n");
        }

        static private void WriteUsage() {
            Console.WriteLine( "\n" +
                "USAGE: xmlpatch <source_xml> <diffgram> <patched_xml>\n\n" +
                "source_xml    name of the file with the original base XML document or fragment\n" +
                "diffgram      name of the file with the XDL diffgram\n" +
                "patched_xml   name of the file the patched XML document for fragment will be saved to\n"
                );
        }
    }
}
