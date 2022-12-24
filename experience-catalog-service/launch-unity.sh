#!/bin/sh

# $1 = experience ID
# $2 = experience name

mkdir /usr/local/bin/unity
tar -xzf $1 --directory /usr/local/bin/unity

#make username a parameter
chown -R solipsistadmin /usr/local/bin/unity
find /usr/local/bin/unity -maxdepth 1 -name \*.x86_64 -exec chmod +x {} \;

# Set up service
sed -i "s/<EXPERIENCE_NAME>/${2}/g" unity.service
cp unity.service /etc/systemd/system

systemctl start unity