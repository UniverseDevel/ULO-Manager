```
ULO Controller v1.0.0.0

Usage:
  ./ulo.sh [-qh] <ulo_host> <ulo_user> <ulo_pass> <action> <arg 1> <arg N>

Arguments:
  -h - Help, you are reading it now
  -q - Quiet mode, suppress most of the outputs

Actions:
  isrunning - Check if other instance of skript is running
      Arguments:
          None

  checkulo - Check if ULO has any of known problems
      Arguments:
          1. video destination path - path to downloaded videos
                      NOTE: If this argument is not provided or path does not exist
                             or is empty, script will assume video recording is working.

  getmode - Get current ULO camera mode
      Arguments:
          None

  setmode - Set ULO camera mode
      Arguments:
          1. mode - camera recording mode
              a) standard - ULO awake and not recording
              b) spy - ULO awake and recording
              c) alert - ULO asleep and recording

  ispowered - Get info if ULO is powered by electricity from plug
      Arguments:
          None

  getbattery - Get battery capacity
      Arguments:
          None

  iscard - Get info if SD card is inserted into ULO
      Arguments:
          None

  getcardspace - Get SD card free capacity
      Arguments:
          None

  getdiskspace - Get internal memory free capacity
      Arguments:
          None

  movetocard - Move files from internal memory to SD card
      Arguments:
          None
                 NOTE: ULO cannot record during this activity.

  cleandiskspace - Clean files on internal memory
      Arguments:
          1. period - how old/new files should be deleted
                      NOTE: This action requires admin account and ULO cannot
                            record during this activity.
              a) oldestday - Oldest day
              b) oldestweek - Oldest week
              c) oldestyear - Oldest year
              d) latestday - Latest day
              e) latestweek - Latest week
              f) latestyear - Latest year
              g) all - All

  downloadlog - Download ULO log into specified location
      Arguments:
          1. destination path - location where snapshot files should be moved
                                NOTE: Always use absolute paths! Destination
                                      folder must already exist!
          2. retention - how old uploaded files should be removed in hours;
                         if set to 0, no age limit will be used and all
                         files will be kept

  currentsnapshot - Download current snapshot seen by ULO into specified
                    location, if snapshot with same name exists it
                    is overwritten
      Arguments:
          1. destination path - location where snapshot files should be moved
                                NOTE: Always use absolute paths! Destination
                                      folder must already exist!

  downloadvideos - Download all available videos stored in ULO into specified
                   location, if video with same name exists it is skipped
      Arguments:
          1. destination path - location where snapshot files should be moved
                                NOTE: Always use absolute paths! Destination
                                      folder must already exist!
          2. retention - how old uploaded files should be removed in hours;
                         if set to 0, no age limit will be used and all
                         files will be kept

  downloadsnapshots - Download all available snapshots stored in ULO into
                      specified location, if snapshot with same name exists it
                      is skipped
      Arguments:
          1. destination path - location where snapshot files should be moved
                                NOTE: Always use absolute paths! Destination
                                      folder must already exist!
          2. retention - how old uploaded files should be removed in hours;
                         if set to 0, no age limit will be used and all
                         files will be kept

  testavailability - Test for device availability
      Arguments:
          1. host - hostname of device you want to check if available

  checkavailability - Check for device availability and set proper mode
      Arguments:
          1. mode if true - camera recording mode if conditions are met
              a) standard - ULO awake and not recording
              b) spy - ULO awake and recording
              c) alert - ULO asleep and recording
          2. mode if false - camera recording mode if conditions are not met
              a) standard - ULO awake and not recording
              b) spy - ULO awake and recording
              c) alert - ULO asleep and recording
          3. operation - operation to determine how to check devices
              a) and - All devices available to be true
              b) or - Any device available to be true
          4. host1 - hostname of device you want to check if available
          5. host2 - hostname of device you want to check if available
                     (optional)
          6. host3 - hostname of device you want to check if available
                     (optional)
          7. host4 - hostname of device you want to check if available
                     (optional)
          8. host5 - hostname of device you want to check if available
                     (optional)

  callapi - Call API with custom parameters
      Arguments:
          1. api path - path to API module
          2. method - call method [GET|PUT|POST|DELETE|...]
          3. body - body this might be needed by API but is undocumented
          4. JQ JSON filter - JQ JSON filter or . for all

Examples:
  - Download video files
      ./ulo.sh '192.168.0.10' 'test' '123!Abc' 'downloadvideos' '/tmp/ulovideo'

Notes from working with ULO:
    - When using this tool, ULO usually wakes up unless it is in Alert mode.
    - Transfer speeds usually depends on WiFi signal strength or ULOs
      processing power. Due to way how we access files there is not much space
      to make this process faster in this code.
    - Files from ULO memory can be emptied only in standard mode.
    - This tool properly logs in and logs out into ULO; because of this,
      if you use same user for browser access this user will be logged
      out along with this tool at the end of execution.
    - It is advised to create new user without admin privileges to use this
      tool, unless you need to perform tasks that require them. For now
      it seems that ULO can create multiple users, but they sometimes have
      problems to log in.
    - If multiple activities are performed at a same time or their execution
      might overlap, it is advised to create separate ULO users for such
      activities.
    - ULO can perform unintended self reboots which always reset current
      camera mode to standard and therefore ULO will stop recording.
    - In version 10.1308 and maybe earlier, there is a bug where anyone who
      knows about ULO can access all ULO files even when not logged in to ULO,
      when at least one user is logged in to ULO no matter where.
    - In version 10.1308 and maybe earlier, ULO stores WiFi passwords in
      plain text inside its system log which is accessible if requested.
    - There is a possibility of ULOs video recording to stop working correctly,
      in this case ULO will create a video file but it will have 0 bytes.
```