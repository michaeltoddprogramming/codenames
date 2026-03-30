terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket = "codenames-tf-state-978251882572"
    key    = "infrastructure/terraform.tfstate"
    region = "af-south-1"
  }
}

provider "aws" {
  region = var.aws_region
}

resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name = "codenames-vpc"
  }
}

resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.1.0/24"
  map_public_ip_on_launch = true
  availability_zone       = "${var.aws_region}a"

  tags = {
    Name = "codenames-public-subnet"
  }
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.2.0/24"
  map_public_ip_on_launch = true
  availability_zone       = "${var.aws_region}b"

  tags = {
    Name = "codenames-public-subnet-b"
  }
}

resource "aws_subnet" "private" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.11.0/24"
  map_public_ip_on_launch = false
  availability_zone       = "${var.aws_region}a"

  tags = {
    Name = "codenames-private-subnet"
  }
}

resource "aws_subnet" "private_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.12.0/24"
  map_public_ip_on_launch = false
  availability_zone       = "${var.aws_region}b"

  tags = {
    Name = "codenames-private-subnet-b"
  }
}

resource "aws_internet_gateway" "gw" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "codenames-igw"
  }
}

resource "aws_route_table" "public_rt" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "codenames-public-rt"
  }
}

resource "aws_route" "public_internet" {
  route_table_id         = aws_route_table.public_rt.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.gw.id
}

resource "aws_route_table" "private_rt" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "codenames-private-rt"
  }
}

resource "aws_route_table_association" "a" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public_rt.id
}

resource "aws_route_table_association" "b" {
  subnet_id      = aws_subnet.public_b.id
  route_table_id = aws_route_table.public_rt.id
}

resource "aws_route_table_association" "private_a" {
  subnet_id      = aws_subnet.private.id
  route_table_id = aws_route_table.private_rt.id
}

resource "aws_route_table_association" "private_b" {
  subnet_id      = aws_subnet.private_b.id
  route_table_id = aws_route_table.private_rt.id
}

resource "aws_security_group" "ec2_sg" {
  name        = "codenames-ec2-sg"
  description = "Allow SSH, HTTP, HTTPS for game server"
  vpc_id      = aws_vpc.main.id

  ingress {
    description = "SSH"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "codenames-ec2-sg"
  }
}

resource "aws_security_group" "db_sg" {
  name        = "codenames-db-sg"
  description = "Allow PostgreSQL from EC2"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "PostgreSQL from EC2"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.ec2_sg.id]
  }

  ingress {
    description = "PostgreSQL from runner VPC via peering"
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["10.1.0.0/16"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "codenames-db-sg"
  }
}

data "aws_vpc" "runner" {
  cidr_block = "10.1.0.0/16"
}

data "aws_route_table" "runner" {
  filter {
    name   = "tag:Name"
    values = ["codenames-gitlab-runner-rt"]
  }
}

resource "aws_vpc_peering_connection" "main_to_runner" {
  vpc_id      = aws_vpc.main.id
  peer_vpc_id = data.aws_vpc.runner.id
  auto_accept = true

  requester {
    allow_remote_vpc_dns_resolution = true
  }

  accepter {
    allow_remote_vpc_dns_resolution = true
  }

  tags = {
    Name = "codenames-main-to-runner"
  }
}

resource "aws_route" "main_to_runner" {
  route_table_id            = aws_route_table.public_rt.id
  destination_cidr_block    = "10.1.0.0/16"
  vpc_peering_connection_id = aws_vpc_peering_connection.main_to_runner.id
}

resource "aws_route" "private_to_runner" {
  route_table_id            = aws_route_table.private_rt.id
  destination_cidr_block    = "10.1.0.0/16"
  vpc_peering_connection_id = aws_vpc_peering_connection.main_to_runner.id
}

resource "aws_route" "runner_to_main" {
  route_table_id            = data.aws_route_table.runner.id
  destination_cidr_block    = "10.0.0.0/16"
  vpc_peering_connection_id = aws_vpc_peering_connection.main_to_runner.id
}


resource "aws_key_pair" "kp" {
  key_name   = "codenames-key"
  public_key = var.ssh_public_key
}

data "aws_ami" "amazon_linux" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-*-arm64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_instance" "server" {
  ami                    = data.aws_ami.amazon_linux.id
  instance_type          = var.ec2_instance_type
  key_name               = aws_key_pair.kp.key_name
  subnet_id              = aws_subnet.public.id
  vpc_security_group_ids = [aws_security_group.ec2_sg.id]

  user_data = <<-USERDATA
#!/bin/bash
set -euo pipefail
exec > /var/log/user-data.log 2>&1

echo "${var.ssh_public_key}" > /home/ec2-user/.ssh/authorized_keys
chown ec2-user:ec2-user /home/ec2-user/.ssh/authorized_keys
chmod 600 /home/ec2-user/.ssh/authorized_keys

JAVA_URL="https://download.oracle.com/java/26/latest/jdk-26_linux-aarch64_bin.tar.gz"
curl -L -o /tmp/jdk.tar.gz "$JAVA_URL"
mkdir -p /opt/java
tar -xzf /tmp/jdk.tar.gz -C /opt/java --strip-components=1
rm /tmp/jdk.tar.gz

cat > /etc/profile.d/java.sh <<'JEOF'
export JAVA_HOME=/opt/java
export PATH=$JAVA_HOME/bin:$PATH
JEOF

dnf install -y nginx

mkdir -p /etc/nginx/ssl

TOKEN=$(curl -s -X PUT "http://169.254.169.254/latest/api/token" -H "X-aws-ec2-metadata-token-ttl-seconds: 21600")
PUBLIC_IP=$(curl -s -H "X-aws-ec2-metadata-token: $TOKEN" http://169.254.169.254/latest/meta-data/public-ipv4)

openssl req -x509 -nodes -days 365 \
  -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/server.key \
  -out /etc/nginx/ssl/server.crt \
  -subj "/CN=codenames-server" \
  -addext "subjectAltName=IP:$PUBLIC_IP"

cat > /etc/nginx/conf.d/codenames.conf <<'NEOF'
server {
    listen 443 ssl;
    server_name _;

    ssl_certificate     /etc/nginx/ssl/server.crt;
    ssl_certificate_key /etc/nginx/ssl/server.key;
    ssl_protocols       TLSv1.2 TLSv1.3;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;

        proxy_buffering off;
        proxy_cache off;
        proxy_read_timeout 86400s;
    }
}

server {
    listen 80;
    server_name _;
    return 301 https://$host$request_uri;
}
NEOF

cat > /etc/nginx/nginx.conf <<'NGEOF'
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log notice;
pid /run/nginx.pid;
include /usr/share/nginx/modules/*.conf;
events {
    worker_connections 1024;
}
http {
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';
    access_log /var/log/nginx/access.log main;
    sendfile on;
    tcp_nopush on;
    keepalive_timeout 65;
    types_hash_max_size 4096;
    include /etc/nginx/mime.types;
    default_type application/octet-stream;
    include /etc/nginx/conf.d/*.conf;
}
NGEOF

systemctl enable nginx
systemctl start nginx

cat > /etc/systemd/system/codenames.service <<'SEOF'
[Unit]
Description=CodeNames Spring Boot Server
After=network.target

[Service]
Type=simple
User=ec2-user
WorkingDirectory=/home/ec2-user
ExecStart=/opt/java/bin/java -Xmx384m -Xms256m -jar /home/ec2-user/codenames-server.jar
EnvironmentFile=/home/ec2-user/codenames.env
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
SEOF

systemctl daemon-reload
systemctl enable codenames

echo "User data setup complete"
USERDATA

  user_data_replace_on_change = true

  tags = {
    Name = "codenames-server"
  }
}

resource "aws_db_subnet_group" "default" {
  name       = "codenames-db-subnet-group"
  subnet_ids = [aws_subnet.private.id, aws_subnet.private_b.id]

  tags = {
    Name = "codenames-db-subnet-group"
  }
}

resource "aws_db_instance" "codenames_db" {
  allocated_storage      = 20
  storage_type           = "gp2"
  engine                 = "postgres"
  engine_version         = "17.4"
  instance_class         = "db.t4g.micro"
  identifier             = "codenames-db"
  db_name                = var.db_name
  username               = var.db_admin_user
  password               = var.db_admin_password
  skip_final_snapshot    = true
  publicly_accessible    = false
  vpc_security_group_ids = [aws_security_group.db_sg.id]
  db_subnet_group_name   = aws_db_subnet_group.default.name

  tags = {
    Name = "codenames-db"
  }
}

output "ec2_public_ip" {
  value = aws_instance.server.public_ip
}

output "ssh_command" {
  value = "ssh -i ~/.ssh/codenames-key ec2-user@${aws_instance.server.public_ip}"
}

output "db_host" {
  value = aws_db_instance.codenames_db.address
}

output "db_port" {
  value = aws_db_instance.codenames_db.port
}

output "db_name" {
  value = var.db_name
}

output "db_admin_user" {
  value     = var.db_admin_user
  sensitive = true
}

output "db_admin_password" {
  value     = var.db_admin_password
  sensitive = true
}

output "db_user" {
  value     = var.db_user
  sensitive = true
}

output "db_user_password" {
  value     = var.db_user_password
  sensitive = true
}
