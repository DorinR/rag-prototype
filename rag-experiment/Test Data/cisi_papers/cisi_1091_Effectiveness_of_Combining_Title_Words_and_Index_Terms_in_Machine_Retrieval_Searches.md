---
doc_id: 1091
title: Effectiveness of Combining Title Words and Index
Terms in Machine Retrieval Searches
author: Fisher, H.L.
---

Our experiment was based on volume 24 of Nuclear Science
Abstracts (NSA) which contains about 53,000 citations; we
used the generalized file-management system, Master Control,
which can operate in either an inverted or a linear search mode.
The inverted mode uses a table composed of the unique
vocabulary contained in one or more data elements, along with
all record numbers in which each vocabulary word occurs.  For
example, an inverted table constructed on titles will have one
entry for each unique word of every title in the data base, plus
the record numbers in which each vocabulary word occurs.  For
example, an inverted table constructed on titles will have one
entry for each unique word of every title in the data base, plus
the record numbers in which each word was found.  (In Master
Control, a word is defined as any set of characters bounded on
either side by a legal separator such as a blank, period, comma,
colon, etc.)  On the other hand, in a linear search mode the
data element is compared with the profile word, character by
character, which results in a prohibitively time-consuming
process for large data bases.
  We chose the inverted-table technique because of the large
amount of data to be searched.  Individual tables were
constructed from the titles of the articles, NSA index terms, and
titles and index terms combined.  NSA index terms are controlled
by the Euratom Thesaurus, as revised for NSA.
  We used two criteria in the study.  First, the questions had
to be of real interest to laboratory personnel.  Some of the
questions had actually been submitted by other members of the
staff, to be run concurrently on the same data base on an SDI
basis.  The others were especially constructed by the authors
for this experiment.  Second, citations obtained were to be
considered good (or relevant) only if they actually pertained to
the subject in question; otherwise, they were to be considered
"false drops," regardless of the number of words matched
between the profile and the citation.