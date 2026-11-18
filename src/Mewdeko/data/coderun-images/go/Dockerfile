FROM golang:1.23-alpine
ENV GOCACHE=/gocache GOFLAGS=-buildvcs=false GOTOOLCHAIN=local CGO_ENABLED=0
RUN go build std && chmod -R a+rX /gocache
