# XmlDiffPatch
 A repository to upgrade Microsoft's XmlDiffPatch project suite to use .NET v4.0 (VS2010 &amp; later)

On https://msdn.microsoft.com/en-us/library/aa302295.aspx you can find an article on Microsoft's 'XML Diff and Patch GUI Tool'. 

This article shows how to use a 2 Microsoft provided libraries to compare 2 XML documents and generate a diffgram of the differences. It also describes how to use the diffgram to be able to reconstruct the 2nd XML document from the 1st document, etc. There is also a link in the article to an installer (xmldiffgui.msi) that installs the tool described in the article.

Unfortunately the installed project only contains the sources for the described GUI tool, but not the source code for the 2 libraries.

There is also a link to https://msdn.microsoft.com/en-us/library/aa302294.aspx that contains an article on 'Using the XML Diff and Patch Tool in Your Applications'.

In addition to more information and use-cases this article also contains a link to another installer (http://download.microsoft.com/download/xml/Patch/1.0/WXP/EN-US/xmldiffpatch.exe), but this link is actually dead.

Another link on the page goes to https://msdn.microsoft.com/en-us/library/aa302299.aspx that is an article on Microsoft's 'XML Tools Update'. This states that "The XmlDiff and XmlPatch classes, which are available for download from http://apps.gotdotnet.com/xmltools/xmldiff/", but unfortunately this link is dead as well.

In fact the whole gotdotnet.com domain seem to be out of action; for example the discussion board at http://www.gotdotnet.com/community/messageboard/MessageBoard.aspx?id=207 also just goes nowhere!

(Note there are lots of more recent 'working' and very useful links on XML diffgrams, for example https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/dataset-datatable-dataview/diffgrams). Google/bing and you will find!

The actual source code for the 2 libraries are actually installed with XmlDiffPatch.exe, which is still available and can be downloaded from http://download.microsoft.com/download/1/f/1/1f146f9b-2a71-4904-8b91-e2f62d7b64b3/XmlDiffPatch.exe.

However, installing XmlDiffPatch.exe can be challenging, since the installer will not complete if .NET v1.1 is not installed. Since .NET v1.1 is deprecated and obsolete on Windows 7 and later, this means that you need all kinds of work-arounds to install .NET v1.1 (none of which actually worked for me).  

This repository is a solution to the above issues. It combines all the projects from the above 2 installers into a single repository (containing multiple projects), which contains the source code for each library/executable in its own project.

I have made a modest change to the folder structure (split the code into 'Apps' and 'Libs' sub-folders because of a name clash in Microsoft's naming when merging the projects from the 2 installers, but besides the same naming for everything is maintained. The changes to the projects are:
- Migrated the solutions/projects to Visual Studio 2010. (This allows easy migration to later versions of VS)
- Migrated the projects to use .NET v4.0 Platform.
- Specified ReferencePaths to allow EXE projects to use the DLLs from their project locations.
- Specified some command line parameters for debugging.

There are some NET v1.1 obsolete functions being called, that I will address in seperate pull-requests.

If you find any bugs/issues, please open a pull-request!

NOTE that any changes to the code is subject to the MIT License. The original code from Microsoft is subject to the EULA specified in XMLDiff\Doc\XML Diff and Patch EULA.rtf.
