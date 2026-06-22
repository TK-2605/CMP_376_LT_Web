(() => {
  const waitingRoom = document.querySelector('[data-waiting-room]');
  if (waitingRoom) {
    const startAt = new Date(waitingRoom.dataset.startAt).getTime();
    const countdown = waitingRoom.querySelector('[data-wait-countdown]');
    let redirected = false;
    const updateWaiting = () => {
      const remaining = Math.max(Math.floor((startAt - Date.now()) / 1000), 0);
      const hours = String(Math.floor(remaining / 3600)).padStart(2, '0');
      const minutes = String(Math.floor((remaining % 3600) / 60)).padStart(2, '0');
      const seconds = String(remaining % 60).padStart(2, '0');
      if (countdown) countdown.textContent = hours + ':' + minutes + ':' + seconds;
      if (remaining === 0 && !redirected) {
        redirected = true;
        window.location.assign(waitingRoom.dataset.refreshUrl);
      }
    };
    updateWaiting();
    window.setInterval(updateWaiting, 1000);
    return;
  }

  const app = document.querySelector('[data-exam-app]');
  if (!app) return;

  const questions = Array.from(app.querySelectorAll('[data-take-question]'));
  const submitForm = app.querySelector('[data-submit-form]');
  const antiforgeryToken = submitForm.querySelector('input[name="__RequestVerificationToken"]').value;
  const expiresAt = new Date(app.dataset.expiresAt).getTime();
  const totalSeconds = Math.max(Number(app.dataset.durationMinutes || 1) * 60, 1);
  let currentQuestion = 0;
  let isFinishing = false;
  let saveTimer = null;
  const warningTimes = new Map();

  const selectedIds = (question) => Array.from(question.querySelectorAll('input:checked')).map((input) => input.value);
  const answeredCount = () => questions.filter((question) => selectedIds(question).length > 0).length;

  const showQuestion = (index) => {
    currentQuestion = Math.max(0, Math.min(index, questions.length - 1));
    questions.forEach((question, questionIndex) => { question.hidden = questionIndex !== currentQuestion; });
    app.querySelectorAll('[data-take-jump]').forEach((button, buttonIndex) => {
      button.classList.toggle('active', buttonIndex === currentQuestion);
      button.classList.toggle('answered', selectedIds(questions[buttonIndex]).length > 0);
    });
    app.querySelector('[data-take-previous]').disabled = currentQuestion === 0;
    app.querySelector('[data-take-next]').disabled = currentQuestion === questions.length - 1;
    const count = answeredCount();
    app.querySelector('[data-answered-count]').textContent = count;
    app.querySelector('[data-progress-label]').textContent = count + '/' + questions.length;
  };

  const setSaveState = (message, isError) => {
    const label = app.querySelector('[data-save-state]');
    label.textContent = message;
    label.closest('.take-save-state').classList.toggle('error', Boolean(isError));
  };

  const autosave = async (question) => {
    if (isFinishing) return;
    const formData = new FormData();
    formData.append('__RequestVerificationToken', antiforgeryToken);
    formData.append('AttemptId', app.dataset.attemptId);
    formData.append('QuestionId', question.dataset.questionId);
    selectedIds(question).forEach((id) => formData.append('SelectedOptionIds', id));
    setSaveState('Đang lưu đáp án...', false);
    try {
      const response = await fetch(app.dataset.autosaveUrl, { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const data = await response.json();
      if (response.status === 409 || data.locked) {
        isFinishing = true;
        window.location.assign(app.dataset.resultUrl);
        return;
      }
      if (!response.ok) throw new Error(data.message || 'Không thể lưu');
      setSaveState('Đã lưu lúc ' + data.savedAt, false);
    } catch {
      setSaveState('Chưa lưu được, hệ thống sẽ thử lại khi bạn đổi đáp án.', true);
    }
  };

  const recordWarning = async (eventType) => {
    if (isFinishing) return;
    const now = Date.now();
    if (now - (warningTimes.get(eventType) || 0) < 3000) return;
    warningTimes.set(eventType, now);
    const formData = new FormData();
    formData.append('__RequestVerificationToken', antiforgeryToken);
    formData.append('AttemptId', app.dataset.attemptId);
    formData.append('EventType', eventType);
    try {
      const response = await fetch(app.dataset.warningUrl, { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' }, keepalive: true });
      const data = await response.json();
      if (typeof data.warningCount === 'number') app.querySelector('[data-warning-count]').textContent = data.warningCount;
      if (data.locked) {
        isFinishing = true;
        window.location.assign(app.dataset.resultUrl);
      }
    } catch {
      // A transient warning request must not interrupt the exam UI.
    }
  };

  const updateTimer = () => {
    const remaining = Math.max(Math.floor((expiresAt - Date.now()) / 1000), 0);
    const minutes = String(Math.floor(remaining / 60)).padStart(2, '0');
    const seconds = String(remaining % 60).padStart(2, '0');
    app.querySelector('[data-exam-timer]').textContent = minutes + ':' + seconds;
    app.querySelector('[data-timer-progress]').style.width = Math.min(100, (remaining / totalSeconds) * 100) + '%';
    if (remaining === 0 && !isFinishing) {
      isFinishing = true;
      submitForm.submit();
    }
  };

  app.addEventListener('click', (event) => {
    const jump = event.target.closest('[data-take-jump]');
    if (jump) showQuestion(Number(jump.dataset.takeJump));
    if (event.target.closest('[data-take-previous]')) showQuestion(currentQuestion - 1);
    if (event.target.closest('[data-take-next]')) showQuestion(currentQuestion + 1);
    if (event.target.closest('[data-enter-fullscreen]') && document.documentElement.requestFullscreen) {
      document.documentElement.requestFullscreen().catch(() => {});
    }
  });

  app.addEventListener('change', (event) => {
    if (!event.target.matches('.take-option input')) return;
    const question = event.target.closest('[data-take-question]');
    showQuestion(currentQuestion);
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => autosave(question), 250);
  });

  submitForm.addEventListener('submit', (event) => {
    if (isFinishing) return;
    const submitButton = event.submitter || submitForm.querySelector('button[type="submit"]');
    const missing = questions.length - answeredCount();
    const message = missing > 0 ? 'Bạn còn ' + missing + ' câu chưa trả lời. Vẫn nộp bài?' : 'Nộp bài và kết thúc lượt thi?';
    if (submitButton.dataset.confirmBypass !== 'true') {
      event.preventDefault();
      submitButton.dataset.confirm = message;
      submitButton.click();
      return;
    }
    isFinishing = true;
    submitButton.disabled = true;
    submitButton.textContent = 'Đang nộp bài...';
  });

  document.addEventListener('visibilitychange', () => { if (document.hidden) recordWarning('TabHidden'); });
  window.addEventListener('blur', () => recordWarning('WindowBlur'));
  document.addEventListener('fullscreenchange', () => {
    if (app.dataset.requireFullscreen === 'true' && !document.fullscreenElement) recordWarning('FullscreenExited');
  });
  document.addEventListener('copy', () => recordWarning('CopyAttempt'));
  document.addEventListener('paste', () => recordWarning('PasteAttempt'));
  window.addEventListener('beforeunload', (event) => {
    if (!isFinishing) {
      event.preventDefault();
      event.returnValue = '';
    }
  });

  showQuestion(0);
  updateTimer();
  window.setInterval(updateTimer, 1000);
})();
