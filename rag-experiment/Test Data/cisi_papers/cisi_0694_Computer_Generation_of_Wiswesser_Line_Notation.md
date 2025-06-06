---
doc_id: 694
title: Computer Generation of Wiswesser Line Notation
author: Farrell, C.D.
Chauvenet, A.R.
Koniver, D.A.
---

Computer programs developed at the National Institutes of Health (NIH) produce
uncontracted though otherwise canonical Wiswesser Line Notation (WLN) for a  
fairly broad class of compounds.  An associated front end allows a chemist to
communicate with the programs by drawing structures on a Rand Tablet.  The
WLN generation programs accept connection table input, either from a previously
existing file or generated from the Rand Tablet drawing.  The programs recognize
situations which they cannot handle - the output is thus either correct WLN
or a message by which the programs acknowledge their limitations.  In general,
correct WLN will be produced for any compound containing not more than one
nonbenzene ring.  Work is under way to extend this to polycyclic fused ring 
systems.  The philosophy and concepts behind these programs are explained along
with the more interesting algorithmic results.  The role of the WLN-generation
programs in a developing NIH chemical information system is briefly discussed.
The WLN programs are written in Fortran IV and have been developed on a 
PDP-10 computer.