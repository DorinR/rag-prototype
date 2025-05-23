---
doc_id: 511
title: Program Design for Retrospective Searches on Large Data Bases
author: Thiel, L. H.
Heaps, H. S.
---

Retrospective search of large document data bases requires development of 
special techniques for automatic compression of data and minimization of the 
number of input-output operations to the computer accessible files.. Also, the 
computer program should be designed to require a relatively small amount of 
internal memory..
   The present paper contains a description of the structure of a program that 
meets the above requirements.. The vocabulary of the data base is automatically 
expressed in terms of 8, 16 and 24 bit codes chosen to point to the natural 
spelling in a dictionary.. Thus file size is reduced without the necessity for
extensive processing for decoding.. Use of a compressed bit string inverted
index greatly reduces search time, and a storage management system enables long
strings to be processed with use of a limited amount of internal storage..
Creation of "reduced" files and tables is an important feature of a program; it
allows the files needed only by specific phases of the program to be designed 
to use a relatively small amount of internal storage and input-output time..