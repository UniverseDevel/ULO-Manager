#!/usr/bin/env bash
#set -x

host="${1}"
username="${2}"
password="${3}"
action="${4}"
arg1="${5}"
arg2="${6}"
arg3="${7}"
arg4="${8}"
#arg5="${9}"

usage() {
  echo "Usage: ./ulo.sh <ulo_host> <ulo_user> <ulo_pass> <action> <arg 1> <arg N>"
  echo "Action:"
  echo "  currentsnapshot <path where to download>"
  echo "  downloadvideos <path where to download>"
  echo "  downloadsnapshots <path where to download>"
  echo "  callapi <API path> <call method [GET|PUT|POST|DELETE|...]> <body this might be needed by API but is undocumented> <JQ JSON filter or . for all>"
}

if [[ -z "${action}" ]]; then
  usage
  exit 0
fi

if ! command -v jq 1>/dev/null 2>&1; then
  echo "ERROR: JQ binary is not installed."
  exit 1
fi

if ! command -v wget 1>/dev/null 2>&1; then
  echo "ERROR: WGET binary is not installed."
  exit 1
fi

auth="Basic $(echo -n "${username}":"${password}" | base64)"
password="******" # We don't have to hold password anymore
output=""

# Usage:
# callapi "${path}" "${method}" "${body}" "${json_filter}"
# echo "${output}"
#
# json_filter - filter in JSON structure, dot (.) can be used to output everything
#               otherwise filter is constructed from element names connected via pipe (|).
#               Detailed documentation how to use syntax in json_filter is here:
#               https://stedolan.github.io/jq/manual/#Basicfilters
#               and online evaluator is here: https://jqplay.org/
callapi() {
  local path="${1}"
  local method="${2}"
  local body="${3}"
  local json_filter="${4}"
  local web_output=""

  web_output="$(curl -s "http://${host}${path}" -X "${method}" -d "${body}" -H "Content-Type: application/json" -H "Authorization: ${auth}")"

  # Check if returned value is valid JSON
  if ! jq -e . >/dev/null 2>&1 <<<"${web_output}"; then
    output="${web_output}"
  else
    output="$(echo "${web_output}" | jq -r "${json_filter}")"
  fi
}

login() {
  callapi "/api/v1/login" "POST" "{ \"iOSAgent\": false }" ".token"
  auth="Bearer ${output}"
}

logout() {
  callapi "/api/v1/logout" "POST" "{}" "."
  auth=""
}

downloadmedia() {
  local extension="${1}"
  local path_to="${2}"
  local init="none"

  echo "${output}" | grep "${extension}" | while read -r video; do
    video_path="$(realpath "$(realpath "${path_to}")/$(dirname "${video}" | sed 's/media//')")"
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
