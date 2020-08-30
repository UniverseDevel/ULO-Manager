Hello everyone,

Now, we all know that ULO has some serious technical issues, but some of those are also security issues. Here are some that I found during my poking around when creating this library.

http://<ULO_IP>/logs/system.txt - contains plain/text passwords of WiFi netowrks, search for: Wifi::updateConfiguration(QJsonObject), log is never rotated so it lasts forever even after factory reset
Can connect to ULO without password when someone else is connected already