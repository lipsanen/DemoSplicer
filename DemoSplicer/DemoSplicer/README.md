# DemoSplicer
A program for combining multiple new engine Source demos into one big demo for segmented runs.

# Can someone use this for splicing SS runs?
1. Demos aren't sufficient proof for runs anyway, if they were you could also segment your run by map.
2. Demos spliced using this program can be detected.

# Usage
Invoke the program from the command line.

With individual demos:

DemoSplicer.exe output.dem demo1.dem demo2.dem demo3.dem

With a folder:

DemoSplicer.exe output.dem folder

In either case the demos are ordered either numerically if all the filenames are numbers, alphabetically otherwise.

# TODO
Fix:

The weird crouching bug after demo transitions while holding crouch

Some particle related errors after demo transitions

Compability with VolvoWrench