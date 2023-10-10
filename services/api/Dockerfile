# Base Image.
FROM node:18-alpine AS base
# Update system image.
RUN apk update
# Install system image dependencies.
RUN apk add --no-cache libc6-compat

# Build Stage.
FROM base AS build-stage
# Create app directory.
WORKDIR /usr/src/app
# Copy application dependency manifests to the container image.
COPY package*.json ./
# Generate package-lock.json.
RUN npm install --package-lock-only
# Install app dependencies.
RUN npm ci 
# Clean npm cache.
RUN npm cache clean --force
# Bundle app source.
COPY . .
# Build app.
RUN npm run build 

# Production Stage.
FROM base AS production-stage
# Set environment variables.
ENV NODE_ENV production
# Create app directory.
WORKDIR /usr/src/app
# Change ownership to node user.
RUN chown -R node:node /usr/src/app
# create a non-root user.
USER node
# Copy application dependency manifests to the container image.
COPY --from=build-stage --chown=node:node /usr/src/app/package*.json ./
# Copy application dependencies to the container image.
COPY --from=build-stage --chown=node:node /usr/src/app/node_modules ./node_modules
# Copy application build to the container image.
COPY --from=build-stage --chown=node:node /usr/src/app/dist ./dist
# Define an argument for the port with a default value of 3000.
ARG PORT=3000
# Set an environment variable for the port using the ARG value.
ENV PORT=${PORT}
# Expose the port to inform Docker that the container will listen on the specified port.
EXPOSE ${PORT}
# Run the web service on container startup.
CMD [ "node", "dist/main"]