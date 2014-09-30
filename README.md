WinServiceWrapper
=================

A simple service wrapper based on TopShelf

Usage
-----

Edit the .config file to point at the target exe.

If no commands are given, start and stop will simply start and terminate the hosted process.

Running the tests
-----------------

By default services run as the 'Local Service' user; this user must have write access to `C:\Temp`. It would be nice to use `Path.GetTempPath()` but the user that the service runs as is different from the one that runs the tests.
