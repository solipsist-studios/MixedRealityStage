#!/bin/sh

# $1 = experience ID
# $2 = admin username

mkdir /usr/local/bin/unity
tar -xzf $1 --directory /usr/local/bin/unity

chown -R $2 /usr/local/bin/unity
find /usr/local/bin/unity -maxdepth 1 -name \*.x86_64 -exec chmod +x {} \;

# Set up service
file=$(ls /usr/local/bin/unity/*.x86_64)
exp_name=$(basename $file .x86_64)
sed -i "s/<EXPERIENCE_NAME>/$exp_name/g" unity.service
cp unity.service /etc/systemd/system

systemctl start unity