#!/bin/sh

tar -xzf $1
find . -maxdepth 1 -name \*.x86_64 -exec {} \;