#!/bin/sh
# Отвечает git на Username/Password. Значения берёт из окружения, не из git.
case "$1" in
  *[Uu]sername*) printf '%s\n' "${GIT_ASKPASS_USER-}" ;;
  *) printf '%s\n' "${GIT_ASKPASS_PASSWORD-}" ;;
esac
