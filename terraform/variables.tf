variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "af-south-1"
}

variable "ec2_instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t4g.micro"
}

variable "db_name" {
  description = "Database name"
  type        = string
  default     = "codenames"
}

variable "db_admin_user" {
  description = "Database admin username (used by Flyway migrations)"
  type        = string
}

variable "db_admin_password" {
  description = "Database admin password (used by Flyway migrations)"
  type        = string
  sensitive   = true
}

variable "db_user" {
  description = "Database app username (used by the server at runtime)"
  type        = string
}

variable "db_user_password" {
  description = "Database app password (used by the server at runtime)"
  type        = string
  sensitive   = true
}

variable "ssh_public_key" {
  description = "SSH public key for EC2 access"
  type        = string
}

variable "jwt_secret" {
  description = "JWT signing secret for the Spring Boot server"
  type        = string
  sensitive   = true
}

