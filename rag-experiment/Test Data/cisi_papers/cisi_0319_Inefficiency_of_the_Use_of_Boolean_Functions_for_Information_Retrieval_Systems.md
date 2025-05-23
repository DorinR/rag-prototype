---
doc_id: 319
title: Inefficiency of the Use of Boolean Functions for Information Retrieval Systems
author: Verhoeff, J.
Goffman, W.
Belzer, J.
---

In this note we attempt to point out why boolean functions
are, in general, not applicable in information retrieval
systems.
  First, we wish to stress that a system, which supposedly
is to serve a certain purpose, has to try to optimize some
overall performance rather than certain detailed parts of
it.  This situation is, of course, well known. 
  Saying that a system should cater to an optimal performance
implies that the reward varies with different circumstances.
That is, there may always be some customers who will not agree
that the system's output is satisfactory.  However, these
should be relatively few.  In the case of an information
retrieval system, let us consider one whose function is to
furnish a reference list as a reaction to a question.  
So, if we have a set of documents S and a set of questions
Q, the system has to assign to each question q, an answer A(q)
which is a subset of S.  Naturally, this answer cannot be
chosen arbitrarily; it should reflect a relation between
the question and the resulting reference list.  Usually
one says that the documents in the list are relevant to
the question.  More precisely stated, we assume that the
enquirer expects a certain reference list, namely the one
he would have procured had he himself probed the documents
in the set.