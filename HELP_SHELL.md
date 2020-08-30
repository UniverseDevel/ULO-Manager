```
Usage: ./ulo.sh <ulo_host> <ulo_user> <ulo_pass> <action> <arg 1> <arg N>
Action:
  currentsnapshot <path where to download>
  downloadvideos <path where to download>
  downloadsnapshots <path where to download>
  callapi <API path> <call method [GET|PUT|POST|DELETE|...]> <body this might be needed by API but is undocumented> <JQ JSON filter or . for all>
```