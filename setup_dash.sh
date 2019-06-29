#!/bin/bash

if [ "$1" == "adduser" ] ; then
	if [ "$2" == "" ]; then echo Missing username.; exit; fi
	htpasswd /etc/apache2/.htpasswd $2
	exit
fi

if [ "$1" == "noip" ] ; then
	wget http://www.no-ip.com/client/linux/noip-duc-linux.tar.gz
	tar xf noip-duc-linux.tar.gz
	cd noip-2.1.9-1
	make install
	noip2
	exit
fi

if [ "$1" == "run" ] ; then
	nohup dotnet run kestrel > /dev/null 2>&1 &
	exit
fi

if [[ "$1" != "" && "$1" != "nocore" ]] ; then
	echo $0 [adduser USERNAME] [noip] [nocore]
	exit
fi

if [ "$1" != "nocore" ] ; then
	if [ ! -e /etc/apt/sources.list.d/dotnetdev.list ] ; then
		sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ xenial main" > /etc/apt/sources.list.d/dotnetdev.list'
		apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893
	fi
fi
apt-get update

apt-get install -y apache2 openssl unzip
if [ "$1" != "nocore" ] ; then
	apt-get install -y dotnet-dev-1.0.4
fi
a2enmod ssl proxy proxy_http

if [ ! -e /etc/ssl/localcerts/apache.pem ] ; then
	mkdir -p /etc/ssl/localcerts
	openssl req -batch -new -x509 -days 365 -nodes -out /etc/ssl/localcerts/apache.pem -keyout /etc/ssl/localcerts/apache.key
fi

echo Creating htpasswd file with scraper user...
htpasswd -c /etc/apache2/.htpasswd scraper 

tail -37 $0 | cut -c2- > /etc/apache2/sites-available/ssl.conf
a2ensite ssl

apachectl restart

#<IfModule mod_ssl.c> 
#        <VirtualHost *:443>
#                ServerAdmin webmaster@localhost
#
#                DocumentRoot /var/www/html
#                ErrorLog ${APACHE_LOG_DIR}/error.log
#                CustomLog ${APACHE_LOG_DIR}/access.log combined
#                ProxyPreserveHost On
#                ProxyPass /WebApp http://127.0.0.1:5000/WebApp
#                ProxyPassReverse /WebApp http://127.0.0.1:5000/WebApp
#
#                <Proxy *>
#                 Order deny,allow
#                 Allow from all
#                 Authtype Basic
#                 Authname "Password Required"
#                 AuthUserFile /etc/apache2/.htpasswd
#                 Require valid-user
#                </Proxy>
#
#                SSLEngine on
#                SSLCertificateFile      /etc/ssl/localcerts/apache.pem
#                SSLCertificateKeyFile /etc/ssl/localcerts/apache.key
#                <FilesMatch "\.(cgi|shtml|phtml|php)$">
#                                SSLOptions +StdEnvVars
#                </FilesMatch>
#                <Directory /usr/lib/cgi-bin>
#                                SSLOptions +StdEnvVars
#                </Directory>
#
#                BrowserMatch "MSIE [2-6]" \
#                                nokeepalive ssl-unclean-shutdown \
#                                downgrade-1.0 force-response-1.0
#                # MSIE 7 and newer should be able to use keepalive
#                BrowserMatch "MSIE [17-9]" ssl-unclean-shutdown
#        </VirtualHost>
#</IfModule> 
