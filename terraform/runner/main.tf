terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket = "codenames-tf-state-978251882572"
    key    = "runner/terraform.tfstate"
    region = "af-south-1"
  }
}

provider "aws" {
  region = var.aws_region
}

# ── 1. VPC + Networking ──

resource "aws_vpc" "runner_vpc" {
  cidr_block           = "10.1.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "${var.runner_name}-vpc"
  }
}

resource "aws_internet_gateway" "runner_igw" {
  vpc_id = aws_vpc.runner_vpc.id

  tags = {
    Name = "${var.runner_name}-igw"
  }
}

resource "aws_subnet" "runner_subnet" {
  vpc_id                  = aws_vpc.runner_vpc.id
  cidr_block              = "10.1.1.0/24"
  map_public_ip_on_launch = true
  availability_zone       = "${var.aws_region}a"

  tags = {
    Name = "${var.runner_name}-subnet"
  }
}

resource "aws_route_table" "runner_rt" {
  vpc_id = aws_vpc.runner_vpc.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.runner_igw.id
  }

  tags = {
    Name = "${var.runner_name}-rt"
  }
}

resource "aws_route_table_association" "runner_rta" {
  subnet_id      = aws_subnet.runner_subnet.id
  route_table_id = aws_route_table.runner_rt.id
}

# ── 2. IAM Role (so runner can run Terraform with AWS access) ──

resource "aws_iam_role" "runner_role" {
  name = "${var.runner_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "runner_ec2" {
  role       = aws_iam_role.runner_role.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2FullAccess"
}

resource "aws_iam_role_policy_attachment" "runner_s3" {
  role       = aws_iam_role.runner_role.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonS3FullAccess"
}

resource "aws_iam_role_policy_attachment" "runner_vpc" {
  role       = aws_iam_role.runner_role.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonVPCFullAccess"
}

resource "aws_iam_role_policy_attachment" "runner_iam_readonly" {
  role       = aws_iam_role.runner_role.name
  policy_arn = "arn:aws:iam::aws:policy/IAMReadOnlyAccess"
}
resource "aws_iam_role_policy_attachment" "runner_rds" {
  role       = aws_iam_role.runner_role.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonRDSFullAccess"
}

resource "aws_iam_instance_profile" "runner_profile" {
  name = "${var.runner_name}-profile"
  role = aws_iam_role.runner_role.name
}

# ── 3. Security Group ──

resource "aws_security_group" "runner_sg" {
  name        = "${var.runner_name}-sg"
  description = "Allow SSH and outbound traffic for GitLab Runner"
  vpc_id      = aws_vpc.runner_vpc.id

  ingress {
    from_port   = 22
    to_port     = 22
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
    Name = "${var.runner_name}-sg"
  }
}

# ── 4. SSH Key Pair ──

resource "tls_private_key" "pk" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

resource "aws_key_pair" "kp" {
  key_name   = "${var.runner_name}-key"
  public_key = tls_private_key.pk.public_key_openssh
}

resource "local_file" "ssh_key" {
  content         = tls_private_key.pk.private_key_pem
  filename        = "${path.module}/private_key.pem"
  file_permission = "0400"
}

# ── 5. Runner EC2 Instance ──

data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"]

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_instance" "gitlab_runner" {
  ami                  = data.aws_ami.ubuntu.id
  instance_type        = var.instance_type
  key_name             = aws_key_pair.kp.key_name
  subnet_id            = aws_subnet.runner_subnet.id
  iam_instance_profile = aws_iam_instance_profile.runner_profile.name

  vpc_security_group_ids = [aws_security_group.runner_sg.id]

  user_data = templatefile("${path.module}/user_data.sh", {
    registration_token = var.gitlab_registration_token
    runner_tags        = "aws-runner,docker"
  })

  tags = {
    Name = var.runner_name
  }
}

# ── 6. Outputs ──

output "public_ip" {
  value = aws_instance.gitlab_runner.public_ip
}

output "ssh_command" {
  value = "ssh -i private_key.pem ubuntu@${aws_instance.gitlab_runner.public_ip}"
}
