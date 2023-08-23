#!/bin/bash
 
cd ParallelOriginGameServer/bin/Release/net6.0/logs/
LATESTLOGFILE=$(ls -Art | tail -n 1)
tail -f $LATESTLOGFILE
