(() => {
  const app = document.querySelector('[data-chat-app]');
  if (!app || !window.signalR) return;

  const messages = app.querySelector('[data-chat-messages]');
  const empty = app.querySelector('[data-chat-empty]');
  const form = app.querySelector('[data-chat-form]');
  const input = app.querySelector('[data-chat-input]');
  const send = app.querySelector('[data-chat-send]');
  const status = app.querySelector('[data-chat-status]');
  const error = app.querySelector('[data-chat-error]');
  const roomType = app.dataset.roomType;
  const roomId = Number(app.dataset.roomId);
  messages.scrollTop = messages.scrollHeight;

  const setConnected = (connected, label) => {
    status.classList.toggle('is-online', connected);
    status.lastChild.textContent = ` ${label}`;
    input.disabled = !connected;
    send.disabled = !connected;
  };

  const showError = (message) => {
    error.textContent = message;
    error.hidden = !message;
  };

  const appendMessage = (payload) => {
    empty?.remove();
    const item = document.createElement('article');
    item.className = 'chat-message';
    const heading = document.createElement('div');
    const sender = document.createElement('strong');
    const time = document.createElement('time');
    const content = document.createElement('p');
    sender.textContent = payload.sender;
    time.textContent = new Date(payload.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    content.textContent = payload.message;
    heading.append(sender, time);
    item.append(heading, content);
    messages.append(item);
    messages.scrollTop = messages.scrollHeight;
  };

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/quiz-chat')
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on('ReceiveMessage', appendMessage);
  connection.onreconnecting(() => setConnected(false, 'Đang kết nối lại'));
  connection.onreconnected(async () => {
    await connection.invoke('JoinRoom', roomType, roomId);
    setConnected(true, 'Đang trực tuyến');
  });
  connection.onclose(() => setConnected(false, 'Mất kết nối'));

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const message = input.value.trim();
    if (!message) return;
    showError('');
    send.disabled = true;
    try {
      await connection.invoke('SendMessage', roomType, roomId, message);
      input.value = '';
      input.focus();
    } catch (exception) {
      showError(exception?.message || 'Chưa gửi được tin nhắn.');
    } finally {
      send.disabled = connection.state !== signalR.HubConnectionState.Connected;
    }
  });

  input.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      form.requestSubmit();
    }
  });

  connection.start()
    .then(() => connection.invoke('JoinRoom', roomType, roomId))
    .then(() => setConnected(true, 'Đang trực tuyến'))
    .catch(() => {
      setConnected(false, 'Không thể kết nối');
      showError('Không thể mở kết nối chat. Vui lòng tải lại trang.');
    });
})();
