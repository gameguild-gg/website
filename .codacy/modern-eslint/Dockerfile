# Hosted-only derivative: retain the pinned scanner and repair its loader's
# deprecated missing-file sentinel. No packages are installed or upgraded.
FROM codacy/codacy-eslint9@sha256:cd4008cb541fb7f3ef6e5c11198cffd6845ef7168bf8eab1889670576c846c06

COPY repair-loader.cjs /opt/gameguild-codacy/repair-loader.cjs
RUN node /opt/gameguild-codacy/repair-loader.cjs /app/node_modules /opt/gameguild-codacy/loader-repair.json
