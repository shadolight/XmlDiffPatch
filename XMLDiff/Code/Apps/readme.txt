***************************************************************
Microsoft XML Diff and Patch 1.0 Samples
Readme File
                    
***************************************************************
(c) Copyright Microsoft Corporation, 2002. All rights reserved.
***************************************************************

This folder contains sources of the 3 samples:

1. XmlDiff.exe  - command line utility that can be used to  produce a diffgram for 2 arbitrary xml files. It supports all the options for the xml files comparison that the XmlDiff class supports. 

Build: XmlDiff\XmlDiff.sln
Usage:   xmldiff -?
Execute: xmldiff.exe [options] <source.xml> <target.xml> <diffgram.xml>


2. XmlPatch.exe - command line utility that can be used to  apply a diffgram to a source document to recreate the changed document.

Build:		XmlPatch\XmlPatch.sln
Usage:		xmlpatch -?
Execute:	xmlpatch <source_xml> <diffgram> <patched_xml>


3. XmlDiffView.exe - command line utility that can be used to  produce the html representation of the diffgram for the 2 input xml documents. Consists of 2 projects
	- XmlDiffView - assembly used to browse the diffgram
	- XmlDiffViewApp - executable that produces html representation

Build: 		XmlDiffView.sln
Usage:		XmlDiffView - ?
Execute:	XmlDiffView [options] sourceXmlFile changesXmlFile resultHtmlViewFile


4. XmlDiffGui.exe - Windows GUI utility that can be used to  produce the html representation of the diffgram for the 2 input xml documents.

Build: 		XmlDiffViewGui.sln
Usage:		XmlDiffViewGui (the XML files to compare/diff are selected via the GUI).
Execute:	XmlDiffViewGui