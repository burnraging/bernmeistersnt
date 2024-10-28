---
author: Bernie
operator: Bernie
---

# The Bernmeister's New Testament

## Overview

This GitHub is the distribution point for a translation of the New
Testament (NT) called *The Bernmeister's New Testament* (TBNT). The
control copy/master copy of this translation is 
`./releases/ms-word/the-bernmeisters-new-testament-v1-unabridged.docx`.
All other formats are ultimately derived from this source.
I've written some tools in C# to convert between formats, which are explained below.

To be precise, the actual control copy of the TBNT is the unabridged
version. There are two versions: an abridged (which is simply called
*The Bernmeister's New Testament*) and an unabridged (which is called
*The Bernmeister's New Testament, Unabrided*). The abridged version is
the same as the unabridged except for the exclusion of the book
introductions and the footnotes.

The Bible formats which TBNT have been converted to are USX3.x and
Zephania.

## Directories

`docs` Instructions for using the tools

`releases` Where the actual translations live

**`tools` The tools I created for converting from one format to
another**

## Tools

The tools are written with Visual Studio Community Edition 2022. There
are only console apps for these, and no .exe's for a couple of reasons.
The user can make his or her own .exe's in VS by pulling up the VS
solution and selecting the project(s) he or she desires to make, then
right-clicking on it and selecting publish. I recommend you make it a
self-contained file. It should build on any .NET later than .NET core,
whatever. These allow you to build a linux target as well. These tools
should have no problems running on linux.

All the tools follow a familiar Program.cs pattern which contains the
Main() method. Should the user like, he or she can simply run the tools
in the VS debugger and skip building the .exe's. If that is the case, he
or she will have to changed the args in Program.cs for DEBUG mode, to
manually fix the command line parameters for DEBUG mode.

### Usx-gen

Usx-gen is the main tool for converting TBNT to USX3.x format. The
conversion takes a few steps to accomplish. First, TBNT Unabridged is
modified by doing selective search & replace operations; the .txt result
is stored to a text file. Second, this text file becomes the input to
usx-gen, which takes this text file and converts it to the several .usx
files which form the usx output. Usx-gen has a switch to created either
abridged or unabridged. Detailed instructions for doing the .docx to
.txt search & replace, etc. are in the docs directory.

### Verse-numbering

This is a crude (plenty of bugs) tool for running sanity checks against
TBNT .txt file, again specially modified with search & replaces (but
unfortunately done differently than usx-gen). It sanity checks verse
numbering and a certain amount of the footnotes. Be warned, it's noisy.

### Usxw2-to-usx3

Though not needed for TBNT conversion, I created it anyways. It will
upgrade the USX2.x XML files to USX3.x. The other tools all use USX3.x.

### Usx3-to-Zephania

A generic converter. TBNT in Zephania format was so created by first
converting it to USX3.x, then from there to Zephania.
