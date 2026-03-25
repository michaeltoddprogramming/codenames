#!/bin/bash
set -e

apt-get update -y
apt-get install -y curl unzip docker.io

systemctl start docker
systemctl enable docker
usermod -aG docker ubuntu

curl -L "https://packages.gitlab.com/install/repositories/runner/gitlab-runner/script.deb.sh" | bash
apt-get install -y gitlab-runner

gitlab-runner register \
  --non-interactive \
  --url "https://bbdgitlab.bbd.co.za/" \
  --token "${registration_token}" \
  --executor "docker" \
  --docker-image "alpine:latest" \
  --description "codenames-aws-runner"

gitlab-runner start
