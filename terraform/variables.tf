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

variable "db_username" {
  description = "Database admin username"
  type        = string
}

variable "db_password" {
  description = "Database admin password"
  type        = string
  sensitive   = true
}
