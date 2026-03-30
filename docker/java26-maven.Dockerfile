FROM ubuntu:24.04

RUN apt-get update && apt-get install -y curl maven && rm -rf /var/lib/apt/lists/*

ARG ARCH=aarch64
RUN curl -L -o /tmp/jdk.tar.gz "https://download.oracle.com/java/26/latest/jdk-26_linux-${ARCH}_bin.tar.gz" \
    && mkdir -p /opt/java \
    && tar -xzf /tmp/jdk.tar.gz -C /opt/java --strip-components=1 \
    && rm /tmp/jdk.tar.gz

ENV JAVA_HOME=/opt/java
ENV PATH=$JAVA_HOME/bin:$PATH

RUN java -version && mvn -version
