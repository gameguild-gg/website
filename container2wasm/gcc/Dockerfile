FROM riscv64/alpine:3.21

# install gcc compiler
RUN apk add --no-cache build-base

# test if gcc can compile a simple c++ hello world program and check if the output is correct. abort if not
RUN echo '#include <iostream>' > hello.cpp && \
    echo 'int main() { std::cout << "Hello, World!" << std::endl; return 0; }' >> hello.cpp && \
    g++ hello.cpp -o hello && \
    ./hello | grep -q "Hello, World!" && \
    rm hello.cpp hello

VOLUME /project

WORKDIR /project

# set the default command to run when the container starts
CMD ["bash"]