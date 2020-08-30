Hello everyone,

Some time ago I have been playing around with ULO to create C# library that will allow me to use ULOs API, which for now is still undocumented by author. The main reason for me was, to create a tool that will allow me to download video files from ULOs memory to my network storage, set ulo mode and all this to be an open source project for people to use. Once I found that files can be accessed from web, I know there is a way how to programatically access these files as well. From web browsers network monitor I was able to scrape a lot for API calls in the background to get a little idea, how ULO works behind the scenes. ULO seems to use OAuth 2.0 authentication which in exchange for username and password will return Bearer token which can be then used for all other calls into this API until logged out or timmed out. API seems to be REST API and uses JSON for most of its communication. Not only that but once you are logged in and session exists, you can access web files via apache file index. Once I had a working prototype, I started to play around and created whole solution for downloading, storing and upkeeping media files from ULO. Currently this library supports upload to NFS, FTP and storing on local filesystem as well. It is also possible to set retention to remove old media files. When I had working library like that, I decided that I will also create a little console binary that can use this library with as little effort as possible for such tool. My first problem with ULO (well, lets say major technical problem as there were many other minor issues) was, that it was turning alert mode off on its own and even support confirmed that this can happen when ULO reboots, but they don't have any complains from other users about this so they cannot confirm if this is an real issue, but they will look into it. This was year and a half ago and fix never came as the problem is happening till this day. Therefore, I used ULOs API to set correct mode based on availability of my phone on network via contoller binary and windows scheduler. I wish they had kept their promise and brought ULO as open platform, we would have fixed the problems on our own by now.

Ok, now when story time is over, lets get to technicalities.

ULO Controller application: [ULOController/bin/Release](ULOController/bin/Release)  
ULO library: [ULO/bin/Release](ULO/bin/Release)  
ULO shell script: [ULO/ulo.sh](ULO/ulo.sh)  

Bare in mind, that I am learning C# and this is not a professional product and there is no warranty that it will work. Using this tool is purely at your own risk. Also I have to state that I am in no way affialiated with Mu Design Sàrl. Also, this tool is not aimed for beginner users, it might require a little knowledge of C#, JSONPath, API, networking or Windows CMD usage.

All usernames, passwords or IP addresses below are examples and should not be concidered to be default or safe.

== Security WARNING

Before considering putting ULO into your network read this as a security warning.

[SECURITY_WARNING.md](SECURITY_WARNING.md)  

== How to use ULO shell script

Full help file: [HELP_SHELL.md](HELP_SHELL.md)  

./ulo.sh - show help information with all commands and arguments, this is your main go to place for basic usage

Basic usage:

./ulo.sh <ulo_host> <ulo_user> <ulo_pass> <action> <arg1> <argN>

Here is the list of all commands in example how to use them:
./ulo.sh '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'currentsnapshot' './current'
./ulo.sh '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'downloadsnapshots' './snapshot'
./ulo.sh '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'downloadvideos' './video'

There is also advanced function 'callapi' which allows you to directly call ULOs API and provides you with response:

./ulo.sh <ulo_host> <ulo_user> <ulo_pass> 'callapi' <API path> <call method [GET|PUT|POST|DELETE|...]> <body this might be needed by API but is undocumented> <JQ JSON filter or . for all>

./ulo.sh '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/files/media' 'GET' '' '.'

More info about JQ JSON Filter can be found here: [https://stedolan.github.io/jq/manual/#Basicfilters](https://stedolan.github.io/jq/manual/#Basicfilters)  
Online evaluator is here: [https://jqplay.org/](https://jqplay.org/)  
Simply use dot (.) for full output if you are not sure.

== How to use ULO Controller library

Full help file: [HELP_LIBRARY.md](HELP_LIBRARY.md)  

./ULOController.exe - show help information with all commands and arguments, this is your main go to place for basic usage (doubleclicking it from explorer will open GUI application)

Basic usage:

./ULOController.exe <ulo_host> <ulo_user> <ulo_pass> <action> <arg1> <argN>

Here is the list of all commands in example how to use them:

./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'getmode'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'setmode' 'standard'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'ispowered'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'getbattery'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'iscard'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'getcardspace'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'getdiskspace'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'movetocard'
./ULOController.exe '192.168.0.10' 'ulo_admin@example.com' 'uloAdminPassword123!' 'cleandiskspace' 'all'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'downloadlog' 'nfs' '\\192.168.0.11\ulo\_logs' '480' 'nfs_user' 'nfsPassword123!'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'currentsnapshot' 'nfs' '\\192.168.0.11\ulo\_current' 'nfs_user' 'nfsPassword123!'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'downloadvideos' 'nfs' '\\192.168.0.11\ulo\_videos' '1' '480' 'nfs_user' 'nfsPassword123!'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'downloadsnapshots' 'nfs' '\\192.168.0.11\ulo\_snapshots' '1' '480' 'nfs_user' 'nfsPassword123!'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'testavailability' '192.168.0.1'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'checkavailability' 'standard' 'alert' 'or' '192.168.0.1' '192.168.0.2' '192.168.0.3' '192.168.0.4' '192.168.0.5'

There is also advanced function 'callapi' which allows you to directly call ULOs API and provides you with response:

./ULOController.exe <ulo_host> <ulo_user> <ulo_pass> 'callapi' <API path> <call method [GET|PUT|POST|DELETE|...]> <body this might be needed by API but is undocumented> <JSON path or $ for all>

./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/mode' 'GET' '' '$'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/state' 'GET' '' '$'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/THIS_DOES_NOT_EXIST' 'GET' '' ''
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/files/stats' 'GET' '' 'internal.freeMB'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/interface/CheckVersionOnCloud' 'POST' '{}' 'status'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/files/delete?removeType=1' 'DELETE' '' '$'
./ULOController.exe '192.168.0.10' 'ulo_user@example.com' 'uloUserPassword123!' 'callapi' '/api/v1/system/log' 'GET' '' '$'

More info about JSON Path can be found here: [https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html](https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html)  
Online evaluator is here: [https://jsonpath.com/](https://jsonpath.com/)  
Simply use dolar sign ($) for full output if you are not sure.

== API notes

Here is also few API URLs that I snatched and there are many, many more when using ULO upside down:

http://192.168.0.10/api/v1/login - POST - Login (created Bearer token to be used for all other calls)
http://192.168.0.10/api/v1/time - PUT - ULOs current time
http://192.168.0.10/api/v1/state - GET - Power information
http://192.168.0.10/api/v1/accessEverywhere - GET - ULO device information
http://192.168.0.10/api/v1/backgroundImage - POST - Get current snapshot from ULO to be used as background
http://192.168.0.10/api/v1/config - GET - List of configured parameters
http://192.168.0.10/api/v1/config/language/languages - GET - List of available languages
http://192.168.0.10/api/v1/users - GET - List of all users
http://192.168.0.10/api/v1/config/time/countries - GET - List of avaialble countries
http://192.168.0.10/api/v1/config/time/zones - GET - List of available time zones
http://192.168.0.10/api/v1/config/wifi/networks - GET - Available WiFi networks
http://192.168.0.10/api/v1/system/log - GET - System log
http://192.168.0.10/api/v1/users/1 - GET - Information about user with ID 1 (usually Admin)
http://192.168.0.10/api/v1/files/stats - GET - Statistic about stoarge
http://192.168.0.10/api/v1/interface/CheckVersionOnCloud - POST - Initiate check for update
http://192.168.0.10/api/v1/files/media - GET - Get list of all media files in all dierctories
http://192.168.0.10/api/v1/files/media?type=snapshot - GET - Get list of media files in all dierctories filtered to snapshots
http://192.168.0.10/api/v1/files/media?type=video - GET - Get list of media files in all dierctories filtered to video
http://192.168.0.10/api/v1/files/media/20190623 - GET - Get list of all media files in directory for specific day
http://192.168.0.10/api/v1/files/media/20190623/snapshotCount - GET - Number of snapshots in this path
http://192.168.0.10/api/v1/files/delete?removeType=6 - DELETE - Delete files on local storage - 0: Oldest day; 1: Oldest week; 2: Oldest year; 3: Last day; 4: Last week; 5: Last year; 6: All time
ws://192.168.0.10/api/v1/live - Binary stream - Live feed, currently unsupported by Controller
http://192.168.0.10/api/v1/logout - POST - Logout (invalidate Bearer token used)

Hope this stuff helps at least somone.

Have a nice day,
Martin