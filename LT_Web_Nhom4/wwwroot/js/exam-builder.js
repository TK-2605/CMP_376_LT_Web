(() => {
  const root = document.querySelector('[data-exam-builder]');
  if (!root) return;

  const questionList = root.querySelector('#questionList');
  const questionTemplate = document.getElementById('questionTemplate');
  const answerTemplate = document.getElementById('answerTemplate');
  const activePanelInput = root.querySelector('[data-active-panel-input]');
  const form = document.getElementById('examBuilderForm');
  let currentQuestion = 0;

  const cards = () => Array.from(questionList.querySelectorAll('[data-question-card]'));
  const escapeHtml = (value) => String(value || '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');

  const buildAnswer = (questionIndex, optionIndex, value = '', checked = false) =>
    answerTemplate.innerHTML
      .replaceAll('__Q__', questionIndex)
      .replaceAll('__O__', optionIndex)
      .replaceAll('__LETTER__', String.fromCharCode(65 + optionIndex))
      .replaceAll('__VALUE__', escapeHtml(value))
      .replace('__CHECKED__', checked ? 'checked' : '');

  const showCurrentQuestion = () => {
    cards().forEach((card, index) => { card.hidden = index !== currentQuestion; });
    root.querySelectorAll('[data-question-jump]').forEach((button, index) => button.classList.toggle('active', index === currentQuestion));
    const previous = root.querySelector('[data-previous-question]');
    const next = root.querySelector('[data-next-question]');
    if (previous) previous.disabled = currentQuestion === 0;
    if (next) next.disabled = currentQuestion >= cards().length - 1;
  };

  const updateScore = () => {
    const total = cards().reduce((sum, card) => {
      const value = Number(card.querySelector('[data-question-score]')?.value || 0);
      return sum + (Number.isFinite(value) ? value : 0);
    }, 0);
    root.querySelectorAll('[data-score-total]').forEach((item) => { item.textContent = total.toFixed(2).replace(/\.00$/, ''); });
    const maxValue = root.querySelector('[data-max-score]')?.value || '0';
    const maxLabel = root.querySelector('[data-max-score-label]');
    if (maxLabel) maxLabel.textContent = maxValue;
  };

  const renderNavigation = () => {
    const navigation = root.querySelector('[data-question-navigation]');
    if (!navigation) return;
    navigation.innerHTML = cards().map((card, index) => {
      const content = card.querySelector('textarea[name$=".Content"]')?.value.trim();
      const answered = card.querySelectorAll('[data-answer-row]').length >= 2;
      return '<button type="button" class="question-number-button' + (index === currentQuestion ? ' active' : '')
        + (content && answered ? ' ready' : '') + '" data-question-jump="' + index + '">' + (index + 1) + '</button>';
    }).join('');
    root.querySelectorAll('[data-question-count]').forEach((item) => { item.textContent = cards().length; });
    const current = root.querySelector('[data-current-question-number]');
    if (current) current.textContent = currentQuestion + 1;
    showCurrentQuestion();
  };

  const reindex = () => {
    cards().forEach((card, questionIndex) => {
      card.dataset.questionIndex = questionIndex;
      card.querySelectorAll('[name]').forEach((field) => {
        field.name = field.name.replace(/Questions\[\d+\]/g, 'Questions[' + questionIndex + ']');
      });
      card.querySelectorAll('[data-answer-row]').forEach((row, optionIndex) => {
        row.querySelectorAll('[name]').forEach((field) => {
          field.name = field.name
            .replace(/Questions\[\d+\]/g, 'Questions[' + questionIndex + ']')
            .replace(/Options\[\d+\]/g, 'Options[' + optionIndex + ']');
        });
        const letter = row.querySelector('.option-letter');
        if (letter) letter.textContent = String.fromCharCode(65 + optionIndex);
      });
    });
    currentQuestion = Math.min(currentQuestion, Math.max(cards().length - 1, 0));
    renderNavigation();
    updateScore();
  };

  const addQuestion = () => {
    const questionIndex = cards().length;
    const answers = [0, 1, 2, 3].map((index) => buildAnswer(questionIndex, index, '', index === 0)).join('');
    const html = questionTemplate.innerHTML.replaceAll('__Q__', questionIndex).replace('__ANSWERS__', answers);
    const wrapper = document.createElement('div');
    wrapper.innerHTML = html.trim();
    questionList.appendChild(wrapper.firstElementChild);
    currentQuestion = cards().length - 1;
    reindex();
  };

  const addAnswer = (card) => {
    const questionIndex = cards().indexOf(card);
    const list = card.querySelector('[data-answer-list]');
    const optionIndex = list.querySelectorAll('[data-answer-row]').length;
    if (optionIndex >= 10) return;
    const wrapper = document.createElement('div');
    wrapper.innerHTML = buildAnswer(questionIndex, optionIndex).trim();
    list.appendChild(wrapper.firstElementChild);
    reindex();
  };

  const duplicateCurrent = () => {
    const source = cards()[currentQuestion];
    if (!source) return;
    const clone = source.cloneNode(true);
    clone.querySelectorAll('input[type="file"]').forEach((input) => { input.value = ''; });
    clone.querySelectorAll('input[name$=".QuestionId"], input[name$=".OptionId"]').forEach((input) => { input.value = ''; });
    source.after(clone);
    currentQuestion += 1;
    reindex();
  };

  const moveCurrent = (direction) => {
    const allCards = cards();
    const card = allCards[currentQuestion];
    const targetIndex = currentQuestion + direction;
    if (!card || targetIndex < 0 || targetIndex >= allCards.length) return;
    if (direction < 0) allCards[targetIndex].before(card);
    else allCards[targetIndex].after(card);
    currentQuestion = targetIndex;
    reindex();
  };

  const setPanel = (name) => {
    root.querySelectorAll('[data-builder-panel]').forEach((panel) => { panel.hidden = panel.dataset.builderPanel !== name; });
    root.querySelectorAll('[data-panel-target]').forEach((button) => {
      const active = button.dataset.panelTarget === name;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
    if (activePanelInput) activePanelInput.value = name;
  };

  const validateQuestion = (card) => {
    const content = card.querySelector('textarea[name$=".Content"]');
    const options = Array.from(card.querySelectorAll('[data-answer-row] input[name$=".Content"]'));
    const correctOptions = card.querySelectorAll('[data-correct-option]:checked');
    const valid = Boolean(content?.value.trim()) && options.filter((option) => option.value.trim()).length >= 2 && correctOptions.length === 1;
    content?.classList.toggle('is-invalid', !content.value.trim());
    options.forEach((option) => option.classList.toggle('is-invalid', !option.value.trim()));
    return valid;
  };

  const showPublishError = (questionIndex) => {
    currentQuestion = questionIndex;
    setPanel('questions');
    renderNavigation();
    const stage = root.querySelector('.question-stage');
    let alert = stage.querySelector('[data-builder-client-error]');
    if (!alert) {
      alert = document.createElement('div');
      alert.className = 'alert alert-danger builder-client-error';
      alert.dataset.builderClientError = '';
      stage.querySelector('.question-stage-header').after(alert);
    }
    alert.textContent = 'Câu hỏi này cần có nội dung, ít nhất hai đáp án và đúng một đáp án đúng trước khi đăng đề.';
    cards()[questionIndex]?.querySelector('textarea[name$=".Content"]')?.focus();
    stage.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
  };

  const validateFileInput = (input, options) => {
    const files = Array.from(input.files || []);
    const invalid = files.length > options.maxCount || files.some((file) => !options.types.includes(file.type) || file.size > options.maxBytes);
    if (!invalid) return;
    input.value = '';
    input.setCustomValidity(options.message);
    input.reportValidity();
    input.setCustomValidity('');
  };

  root.addEventListener('click', (event) => {
    const target = event.target.closest('button');
    if (!target) return;
    if (target.matches('[data-panel-target]')) setPanel(target.dataset.panelTarget);
    if (target.matches('[data-add-question]')) addQuestion();
    if (target.matches('[data-add-answer]')) addAnswer(target.closest('[data-question-card]'));
    if (target.matches('[data-duplicate-question]')) duplicateCurrent();
    if (target.matches('[data-move-question]')) moveCurrent(Number(target.dataset.moveQuestion));
    if (target.matches('[data-question-jump]')) { currentQuestion = Number(target.dataset.questionJump); renderNavigation(); }
    if (target.matches('[data-previous-question]') && currentQuestion > 0) { currentQuestion -= 1; renderNavigation(); }
    if (target.matches('[data-next-question]') && currentQuestion < cards().length - 1) { currentQuestion += 1; renderNavigation(); }
    if (target.matches('[data-remove-question]') && cards().length > 1) { cards()[currentQuestion].remove(); reindex(); }
    if (target.matches('[data-remove-answer]')) {
      const card = target.closest('[data-question-card]');
      const rows = card.querySelectorAll('[data-answer-row]');
      if (rows.length > 2) { target.closest('[data-answer-row]').remove(); reindex(); }
    }
  });

  root.addEventListener('change', (event) => {
    if (event.target.matches('[data-correct-option]') && event.target.checked) {
      const card = event.target.closest('[data-question-card]');
      card.querySelectorAll('[data-correct-option]').forEach((input) => { if (input !== event.target) input.checked = false; });
    }
    if (event.target.matches('[data-max-score], [data-question-score]')) updateScore();
    if (event.target.matches('[data-question-images]')) {
      validateFileInput(event.target, {
        maxCount: 5,
        maxBytes: 5 * 1024 * 1024,
        types: ['image/jpeg', 'image/png', 'image/webp'],
        message: 'Chỉ được chọn tối đa 5 ảnh JPG, PNG hoặc WebP, mỗi ảnh không vượt quá 5 MB.'
      });
    }
    if (event.target.matches('[data-question-videos]')) {
      validateFileInput(event.target, {
        maxCount: 2,
        maxBytes: 100 * 1024 * 1024,
        types: ['video/mp4', 'video/webm', 'video/quicktime'],
        message: 'Chỉ được chọn tối đa 2 video MP4, WebM hoặc MOV, mỗi video không vượt quá 100 MB.'
      });
    }
    renderNavigation();
  });

  root.addEventListener('input', (event) => {
    if (event.target.matches('[data-title-input]')) {
      const title = root.querySelector('[data-builder-title]');
      if (title) title.textContent = event.target.value.trim() || 'Đề thi chưa đặt tên';
    }
    if (event.target.matches('textarea[name$=".Content"], input[name$=".Content"]')) renderNavigation();
    if (event.target.matches('textarea[name$=".Content"], input[name$=".Content"]')) event.target.classList.remove('is-invalid');
    if (event.target.matches('[data-question-score], [data-max-score]')) updateScore();
  });

  const focusFirstFormError = () => {
    const firstError = root.querySelector('.field-validation-error:not(:empty), .validation-summary-errors, .input-validation-error, .is-invalid');
    if (!firstError) return false;
    const inSettings = firstError.closest('[data-builder-panel="settings"]');
    setPanel(inSettings ? 'settings' : 'questions');
    const target = firstError.matches('input, select, textarea')
      ? firstError
      : firstError.closest('.form-field, .question-editor-card')?.querySelector('input, select, textarea');
    window.setTimeout(() => {
      (target || firstError).scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'center' });
      target?.focus?.();
    }, 50);
    return true;
  };

  form?.addEventListener('submit', (event) => {
    if (!form.checkValidity()) {
      focusFirstFormError();
      return;
    }
    if (event.submitter?.value !== 'publish') return;
    const invalidIndex = cards().findIndex((card) => !validateQuestion(card));
    if (invalidIndex >= 0) {
      event.preventDefault();
      showPublishError(invalidIndex);
    }
  });

  setPanel(root.dataset.activePanel === 'settings' ? 'settings' : 'questions');
  reindex();
  focusFirstFormError();
})();
