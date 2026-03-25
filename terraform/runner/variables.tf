variable "aws_region" {
  description = "AWS Region"
  type        = string
  default     = "af-south-1"
}

variable "instance_type" {
  description = "EC2 Instance Type"
  type        = string
  default     = "t3.micro"
}

variable "gitlab_registration_token" {
  description = "GitLab Runner Registration Token"
  type        = string
  sensitive   = true
}

variable "runner_name" {
  description = "Name for the runner and instance"
  type        = string
  default     = "codenames-gitlab-runner"
}
