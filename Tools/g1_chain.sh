#!/bin/bash
cd /Users/sang/GitHub/GuilRandomDefense
for n in 290 291 292 293 294 295 296 297 298 299; do
  extra=""; if [ $((n % 2)) -eq 1 ]; then extra=" support pirate"; fi
  rm -f ClaudeBridge/outbox/g1_$n.txt
  echo "gameshot b$n 75 1600x900 rounds:60 autoloop keeppen sell aim:전설 bosschase mode:보통$extra" > ClaudeBridge/inbox/g1_$n.txt
  until [ -f ClaudeBridge/outbox/g1_$n.txt ] && grep -q "gameshot 결과" ClaudeBridge/outbox/g1_$n.txt; do sleep 10; done
  echo "$n done $(date +%T)" >> ClaudeBridge/g1_chain.log
  sleep 20
done
echo ALLDONE >> ClaudeBridge/g1_chain.log
