﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Media;
using ЧаДо;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ЧаДо
{
    [Serializable]
    public partial class Form1 : Form
    {
        BinaryFormatter binFormatter = new BinaryFormatter();
        bool sound = true;
        bool alive = false; // будет ли работать поток для приема
        UdpClient client;
        const int LOCALPORT = 8001; // порт для приема сообщений
        const int REMOTEPORT = 8001; // порт для отправки сообщений
        const int TTL = 20;
        const string HOST = "235.5.5.11"; // хост для групповой рассылки
        IPAddress groupAddress; // адрес для групповой рассылки
        string userName; // имя пользователя в чате
        public Form1()
        {
            InitializeComponent();

            loginButton.Enabled = true; // кнопка входа
            logoutButton.Enabled = false; // кнопка выхода
            sendButton.Enabled = false; // кнопка отправки
            chatTextBox.ReadOnly = true; // поле для сообщений

            
            groupAddress = IPAddress.Parse(HOST);
            try
            {
                using (var file = new FileStream("users.bin", FileMode.OpenOrCreate))
                {
                    var User = binFormatter.Deserialize(file) as string;
                    if (User != null)
                    {
                        userNameTextBox.Text = User;
                    }
                }
            }
            catch { }
            try
            {
                using (var file = new FileStream("dialogs.bin", FileMode.Open))
                {
                    var dialogs = binFormatter.Deserialize(file) as string;
                    if (dialogs != null)
                    {
                        chatTextBox.Text = dialogs;
                    }
                }
            }
            catch  { }
        }
        // обработчик нажатия кнопки loginButton
        private void loginButton_Click(object sender, EventArgs e)
        {
            userName = userNameTextBox.Text;
            userNameTextBox.ReadOnly = true;

            using(var file = new FileStream("users.bin",FileMode.OpenOrCreate))
            {
                binFormatter.Serialize(file,userName);
            }
                try
                {
                    client = new UdpClient(LOCALPORT);
                    // присоединяемся к групповой рассылке
                    client.JoinMulticastGroup(groupAddress, TTL);

                    // запускаем задачу на прием сообщений
                    Task receiveTask = new Task(ReceiveMessages);
                    receiveTask.Start();

                    // отправляем первое сообщение о входе нового пользователя
                    sound = false;
                    string message = userName + " вошел в чат";
                    byte[] data = Encoding.Unicode.GetBytes(message);
                    client.Send(data, data.Length, HOST, REMOTEPORT);

                    loginButton.Enabled = false;
                    logoutButton.Enabled = true;
                    sendButton.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
        }
        // проигрывание звука нового сообщения
        static void playSimpleSound()
        {
            SoundPlayer simpleSound = new SoundPlayer(Resource1.icq_message);
            simpleSound.Play();
        }
        // метод приема сообщений
        private void ReceiveMessages()
        {
            alive = true;
            try
            {
                while (alive)
                {
                    IPEndPoint remoteIp = null;
                    byte[] data = client.Receive(ref remoteIp);
                    string message = Encoding.Unicode.GetString(data);
                    // добавляем полученное сообщение в текстовое поле
                    this.Invoke(new MethodInvoker(() =>
                    {
                        string time = DateTime.Now.ToShortTimeString();
                        chatTextBox.Text = time + " " + message + "\r\n" + chatTextBox.Text;
                        if (sound) playSimpleSound();
                        else sound = true;
                    }));
                }
            }
            catch (ObjectDisposedException)
            {
                if (!alive)
                    return;
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        // обработчик нажатия кнопки sendButton
        public void sendButton_Click(object sender, EventArgs e)
        {
            try
            {
                sound = false;
                string message = String.Format("{0}: {1}", userName, messageTextBox.Text);
                byte[] data = Encoding.Unicode.GetBytes(message);
                client.Send(data, data.Length, HOST, REMOTEPORT);
                messageTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        // обработчик нажатия кнопки logoutButton
        private void logoutButton_Click(object sender, EventArgs e)
        {
            sound = false;
            ExitChat();
        }
        // выход из чата
        private void ExitChat()
        {
            string message = userName + " покидает чат";
            byte[] data = Encoding.Unicode.GetBytes(message);
            client.Send(data, data.Length, HOST, REMOTEPORT);
            client.DropMulticastGroup(groupAddress);

            alive = false;
            client.Close();

            loginButton.Enabled = true;
            logoutButton.Enabled = false;
            sendButton.Enabled = false;
            userNameTextBox.ReadOnly = false;
        }
        // обработчик события закрытия формы
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (alive)
                ExitChat();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && sendButton.Enabled && !String.IsNullOrWhiteSpace(messageTextBox.Text)) sendButton_Click(this, new EventArgs());
            if (String.IsNullOrWhiteSpace(messageTextBox.Text)) messageTextBox.Clear();
        }

        private void saveTimer_Tick(object sender, EventArgs e)
        {
            using (var file = new FileStream("dialogs.bin",FileMode.OpenOrCreate))
            {
               binFormatter.Serialize(file,chatTextBox.Text);
            }
        }
    }
}