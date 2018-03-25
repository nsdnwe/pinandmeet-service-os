ECHO OFF
if "%1"=="" goto ERROR
c:\7zip\7za a -tzip -r C:\Users\nwe\Dropbox\Backups\PinAndMeetService-%1.zip *.*
DIR C:\Users\nwe\Dropbox\Backups\PinAndMeetService-%1.zip
GOTO OUT
:ERROR
ECHO ---------------------------------------------------------
ECHO ERROR: Zip file name missing. Sample: backup MainPageDone
ECHO ---------------------------------------------------------
:OUT