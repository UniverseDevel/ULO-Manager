#!/usr/bin/env bash
#set -x

host=$1
username=$2
password=$3
action=$4
arg1=$5
arg2=$6
arg3=$7
arg4=$8
arg5=$9

usage () {
        echo "Usage: ./ulo.sh <ulo_host> <ulo_user> <ulo_pass> <action> <arg 1> <arg N>"
        echo "Action:"
        echo "  callapi <API path> <call method [GET|PUT|POST|DELETE|...]> <body this might be needed by API but is undocumented> <jq JSON parse or . for all>"
        echo "  currentsnapshot <path where to download>"
        echo "  downloadvideos <path where to download>"
        echo "  downloadsnapshots <path where to download>"
}

if [[ -z "${action}" ]]; then
        usage
        exit 0
fi

which jq 2>&1 1>/dev/null
if [[ "$?" == "1" ]]; then
        echo "ERROR: JQ binary is not installed."
        exit 1
fi

auth="Basic $(echo -n ${username}:${password} | base64)"
password="******" # We don't have to hold password anymore
output=""

# Usage:
# Online JQ tester: https://jqplay.org/
# callapi "${path}" "${method}" "${body}" "${json}"
# echo "${output}"
callapi () {
        local path=$1
        local method=$2
        local body=$3
        local json=$4
        local web_output=$(curl -s "http://${host}${path}" -X ${method} -d "${body}" -H "Content-Type: application/json" -H "Authorization: ${auth}")

        jq -e . >/dev/null 2>&1 <<<"${web_output}" # Check if returned value is valid JSON
        if [[ "$?" != "0" ]]; then
                output="${web_output}"
        else
                output=$(echo "${web_output}" | jq -r "${json}")
        fi
}

login () {
        callapi "/api/v1/login" "POST" "{ \"iOSAgent\": false }" ".token"
        auth="Bearer ${output}"
}

logout () {
        callapi "/api/v1/logout" "POST" "{}" "."
}

downloadmedia () {
        local extension=$1
        local path_to=$2
        local init="none"

        echo "${output}" | grep "${extension}" | while read video; do
                video_path=$(realpath $(realpath ${path_to})/$(dirname ${video} | sed 's/media//'))
                if [[ "${init}" != "${video_path}" ]]; then
                        echo "Storing media files to: ${video_path}"
                        local init="${video_path}"
                fi
                wget -c -N -P "${video_path}" "https://${host}/${video}" --header="Authorization: ${auth}" --no-check-certificate -q --show-progress
                if [[ "${video}" == *loginPicture\.jpg ]]; then
                        mv "${video_path}/loginPicture.jpg" "${video_path}/snapshot.jpg"
                fi
        done
}

login

case "${action}" in
        callapi)
                # User call
                callapi "${arg1}" "${arg2}" "${arg3}" "${arg4}"
                echo "${output}"
                ;;
        currentsnapshot)
                callapi "/api/v1/backgroundImage" "POST" "{}" ".filename"
                downloadmedia ".jpg" "${arg1}"
                ;;
        downloadvideos)
                callapi "/api/v1/files/media" "GET" "" ".files | .[].files | .[]"
                downloadmedia ".mp4" "${arg1}"
                ;;
        downloadsnapshots)
                callapi "/api/v1/files/media" "GET" "" ".files | .[].files | .[]"
                downloadmedia ".jpg" "${arg1}"
                ;;
        *)
                # Unknown action
                echo "ERROR: Action \"${action}\" is unknown."
                logout
                exit 1
                ;;
esac

logout