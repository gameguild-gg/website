# A hosted contract fixture. This does not replace the active Codacy engine.
FROM codacy/codacy-eslint@sha256:01b7b1444a44d33aba5db47aa0f244c889ab24de35a5960536f8ff2b6ca206f1

USER root
COPY apply-repairs.cjs /opt/gameguild-codacy/apply-repairs.cjs
RUN node /opt/gameguild-codacy/apply-repairs.cjs /node_modules
USER docker
