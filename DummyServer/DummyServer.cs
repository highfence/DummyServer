using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DummyServer
{
    class DummyServer
    {
		public int ServerPort { get; private set; }
		public string Ip { get; private set; }

		private Socket listenSocket;
		private SocketAsyncEventArgs acceptArgs;
		private AutoResetEvent flowControllEvent;

		public DummyServer(string ip = "localhost", int port = 23452)
		{
			Ip = ip;
			ServerPort = port;

			try
			{
				listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				var listenEndPoint = new IPEndPoint(IPAddress.Any, ServerPort);

				listenSocket.Bind(listenEndPoint);
				listenSocket.Listen(1024);

				this.acceptArgs = new SocketAsyncEventArgs();
				this.acceptArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);

				Thread listenThread = new Thread(DoListen);
				listenThread.Start();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		void DoListen()
		{
			this.flowControllEvent = new AutoResetEvent(false);

			while (true)
			{
				this.acceptArgs.AcceptSocket = null;

				bool pending = true;

				try
				{
					pending = listenSocket.AcceptAsync(this.acceptArgs);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
					continue;
				}

				if (pending == false)
				{
					OnAcceptCompleted(null, this.acceptArgs);
				}

				this.flowControllEvent.WaitOne();
			}
		}

		void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				Socket clientSocket = e.AcceptSocket;
				// Nagle 알고리즘 해제인듯
				clientSocket.NoDelay = true;

				SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
				receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceiveCompleted);

				var pending = clientSocket.ReceiveAsync(receiveArgs);
				if (!pending)
				{
					ProcessReceive(receiveArgs);
				}
			}
			else
			{
				Console.WriteLine("Failed to accept cilent : " + e.SocketError);
			}

			this.flowControllEvent.Set();
		}

		void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
		{
			if (e.LastOperation == SocketAsyncOperation.Receive)
			{
				ProcessReceive(e);

				return;
			}
		}

		void ProcessReceive(SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
			{
				var buffer = e.Buffer;

				bool pending = e.ConnectSocket.ReceiveAsync(e);
				if (!pending)
				{
					ProcessReceive(e);
				}
			}
			else
			{
				Console.WriteLine("Process receive error");
			}
		}
    }
}
