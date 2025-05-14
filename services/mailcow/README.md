# Mailcow for GameGuild

This directory contains the Docker Compose configuration for running Mailcow, a full-featured mail server suite that will handle email sending, receiving, and forwarding for the gameguild.gg domain.

## Features

- **Complete email solution**: SMTP, IMAP, POP3, DKIM, DMARC, SPF, and more
- **Authentication and security**: TLS encryption, anti-spam, anti-virus
- **User-friendly web interface**: Manage domains, mailboxes, and settings
- **Email forwarding**: Configure address forwarding through the admin panel
- **Integration with other services**: Other GameGuild microservices can use this for sending emails

## Getting Started

1. Review and modify the `.env` file with your preferred settings
2. Make sure to change the default passwords in production
3. Start Mailcow with:

```bash
docker-compose up -d
```

4. Access the admin interface at https://mail.gameguild.gg (or http://localhost:8080 during development)
5. Default admin credentials:
   - Username: admin
   - Password: moohoo (change immediately after first login)

## Configuration for Microservices

Other GameGuild services can send emails through this Mailcow server using SMTP:

- **SMTP Host**: mail.gameguild.gg
- **SMTP Port**: 587 (with STARTTLS) or 465 (with SSL/TLS)
- **Authentication**: Required (create service accounts in the admin panel)

Example Node.js configuration:

```javascript
// Example using Nodemailer
const nodemailer = require('nodemailer');

const transporter = nodemailer.createTransport({
  host: 'mail.gameguild.gg',
  port: 587,
  secure: false, // true for 465, false for other ports
  auth: {
    user: 'service@gameguild.gg', // create a dedicated service account
    pass: 'your-secure-password'
  }
});
```

## Setting Up Email Forwarding

1. Log in to the Mailcow admin panel
2. Navigate to Mail Setup > Mailboxes
3. Create an email address (e.g., username@gameguild.gg)
4. Go to the user's settings
5. Under "Aliases and Forwards," add forwarding addresses (e.g., external-email@example.com)

You can also create catch-all addresses to forward all emails from a domain to a specific address.

## Security Recommendations

1. Change all default passwords
2. Set up SPF, DKIM, and DMARC records for your domain
3. Keep Mailcow updated regularly
4. Set up regular backups of the data directory

## Maintenance

For backups, the following directories contain important data:

```bash
./data/vmail
./data/db
```

## Integration with Coolify

Since this service is being served by Coolify, make sure to:

1. Configure the correct volumes for persistent data
2. Set up appropriate network rules to allow mail ports
3. Configure DNS records correctly for your domain

For more information about Mailcow, visit [Mailcow Documentation](https://mailcow.github.io/mailcow-dockerized-docs/).