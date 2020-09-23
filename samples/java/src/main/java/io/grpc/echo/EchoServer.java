package io.grpc.echo;

import Microsoft.Telepathy.ProtoBuf.EchoGrpc;
import Microsoft.Telepathy.ProtoBuf.EchoOuterClass;
import io.grpc.Server;
import io.grpc.ServerBuilder;
import io.grpc.stub.StreamObserver;
import java.io.IOException;
import java.util.concurrent.TimeUnit;
import java.util.logging.Logger;

public class EchoServer {
    private static final Logger logger = Logger.getLogger(EchoServer.class.getName());
    private Server server;

    public static void main(String[] args) throws IOException, InterruptedException {
        final EchoServer server = new EchoServer();
        server.start();
        server.blockUntilShutdown();
    }

    private void start() throws IOException {
        /* The port on which the server should run */
        String portEvc = System.getenv("TELEPATHY_SVC_PORT");
        System.out.println("Get env: " + portEvc);
        int port = Integer.parseInt(portEvc);
//        int port = 5000;
        server = ServerBuilder.forPort(port)
                .addService(new EchoImpl())
                .build()
                .start();
        logger.info("Server started, listening on " + port);
        Runtime.getRuntime().addShutdownHook(new Thread() {
            @Override
            public void run() {
                // Use stderr here since the logger may have been reset by its JVM shutdown hook.
                System.err.println("*** shutting down gRPC server since JVM is shutting down");
                try {
                    EchoServer.this.stop();
                } catch (InterruptedException e) {
                    e.printStackTrace(System.err);
                }
                System.err.println("*** server shut down");
            }
        });
    }

    private void stop() throws InterruptedException {
        if (server != null) {
            server.shutdown().awaitTermination(30, TimeUnit.SECONDS);
        }
    }

    /**
     * Await termination on the main thread since the grpc library uses daemon threads.
     */
    private void blockUntilShutdown() throws InterruptedException {
        if (server != null) {
            server.awaitTermination();
        }
    }

    static class EchoImpl extends EchoGrpc.EchoImplBase {

        @Override
        public void echo(EchoOuterClass.EchoRequest req, StreamObserver<EchoOuterClass.EchoReply> responseObserver) {
            try{
                Thread.sleep(req.getDelayTime());
            }
            catch (InterruptedException e){
                System.out.println(e);
            }
            EchoOuterClass.EchoReply reply = EchoOuterClass.EchoReply.newBuilder().setMessage(req.getMessage()).build();
            responseObserver.onNext(reply);
            responseObserver.onCompleted();
        }
    }
}

