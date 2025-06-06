---
doc_id: 706
title: A Graph-Theoretic Algorithm for Matching Chemical Structures
author: Sussenguth, E.H., Jr.
---

There are many chemical retrieval systems which
process the first type of request efficiently.  Most of these
systems are also capable of handling certain fragment
requests; however, the fragments which can be processed
are frequently of a restricted nature.  For example, in
retrieval systems which are based on linear ciphers, only
those fragments which are explicit in the cipher are
readily detected.  To allow a completely general 
specification of fragments it seems inevitable that a
detailed atom-by-atom comparison is required of the query
and library structures.  A technique for making such detailed
comparisons is presented in this report.  This technique
is novel in that it avoids the excessive backtracking ad
restarting required by other atom-by-atom matching
procedures.
  Before giving the details of the proposed algorithm,
some definitions are reviewed and a brief example is
presented to illustrate the over-all concepts.  Then the flow
diagram of the algorithm is explained in terms of additional
examples.  Finally, the mechanization of the algorithm for
a digital computer is discussed.
  This report is a condensed version of the original, which
gives a generalization and comprehensive description of
the algorithm, proofs of convergence and related topics,
and applications other than chemical retrieval systems.