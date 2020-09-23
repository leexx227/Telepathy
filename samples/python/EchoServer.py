from concurrent import futures
import logging
import os
import time

import grpc

import echo_pb2
import echo_pb2_grpc

class Echo(echo_pb2_grpc.EchoServicer):
    def Echo(self, request, context):
        time.sleep(request.delay_time / 1000)
        return echo_pb2.EchoReply(message = 'hello ' + request.message)

def start():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers = 10))
    echo_pb2_grpc.add_EchoServicer_to_server(Echo(), server)
    portEnv = os.environ.get('TELEPATHY_SVC_PORT')
    print('Get env: ' + portEnv)
    port = portEnv
    # port = '5000'
    server.add_insecure_port('0.0.0.0:' + port)
    server.start()
    print('listening on port: ' + port)
    server.wait_for_termination()

if __name__ == '__main__':
    logging.basicConfig()
    start()
    print('server end...')
