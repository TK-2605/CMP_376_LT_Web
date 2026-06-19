(function () {
  const questionList = document.getElementById("questionList");
  const questionTemplate = document.getElementById("questionTemplate");
  const answerTemplate = document.getElementById("answerTemplate");
  const addQuestionButton = document.getElementById("addQuestionButton");
  const addSampleQuestionButton = document.getElementById("addSampleQuestionButton");

  if (!questionList || !questionTemplate || !answerTemplate) {
    return;
  }

  function buildAnswer(questionIndex, optionIndex, value, checked) {
    return answerTemplate.innerHTML
      .replaceAll("__Q__", questionIndex)
      .replaceAll("__O__", optionIndex)
      .replaceAll("__VALUE__", escapeHtml(value || ""))
      .replace("__CHECKED__", checked ? "checked" : "");
  }

  function buildQuestion(sample) {
    const questionIndex = questionList.querySelectorAll("[data-question-card]").length;
    const answers = [
      buildAnswer(questionIndex, 0, sample ? "Đáp án A" : "", true),
      buildAnswer(questionIndex, 1, sample ? "Đáp án B" : "", false),
      buildAnswer(questionIndex, 2, sample ? "Đáp án C" : "", false),
      buildAnswer(questionIndex, 3, sample ? "Đáp án D" : "", false)
    ].join("");

    const html = questionTemplate.innerHTML
      .replaceAll("__Q__", questionIndex)
      .replaceAll("__NUMBER__", questionIndex + 1)
      .replace("__ANSWERS__", answers);

    const wrapper = document.createElement("div");
    wrapper.innerHTML = html.trim();
    const card = wrapper.firstElementChild;
    if (sample) {
      const content = card.querySelector(".question-content");
      if (content) {
        content.value = "Nhập nội dung câu hỏi mẫu";
      }
    }

    questionList.appendChild(card);
    reindexQuestions();
    card.scrollIntoView({ behavior: "smooth", block: "center" });
  }

  function reindexQuestions() {
    const cards = questionList.querySelectorAll("[data-question-card]");
    cards.forEach((card, questionIndex) => {
      const number = card.querySelector(".question-number");
      if (number) {
        number.textContent = "Câu hỏi " + (questionIndex + 1);
      }

      card.querySelectorAll("[name]").forEach((field) => {
        field.name = field.name
          .replace(/Questions\[\d+\]/g, "Questions[" + questionIndex + "]")
          .replace(/Options\[\d+\]/g, function (match) {
            return match;
          });
      });

      const answers = card.querySelectorAll("[data-answer-row]");
      answers.forEach((answer, optionIndex) => {
        answer.querySelectorAll("[name]").forEach((field) => {
          field.name = field.name
            .replace(/Questions\[\d+\]/g, "Questions[" + questionIndex + "]")
            .replace(/Options\[\d+\]/g, "Options[" + optionIndex + "]");
        });
      });
    });
  }

  function addAnswer(card) {
    const questionIndex = Array.from(questionList.querySelectorAll("[data-question-card]")).indexOf(card);
    const answerList = card.querySelector("[data-answer-list]");
    const optionIndex = answerList.querySelectorAll("[data-answer-row]").length;
    const wrapper = document.createElement("div");
    wrapper.innerHTML = buildAnswer(questionIndex, optionIndex, "", false).trim();
    answerList.appendChild(wrapper.firstElementChild);
    reindexQuestions();
  }

  function duplicateQuestion(card) {
    const clone = card.cloneNode(true);
    questionList.insertBefore(clone, card.nextSibling);
    reindexQuestions();
  }

  function escapeHtml(value) {
    return value
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  addQuestionButton?.addEventListener("click", function () {
    buildQuestion(false);
  });

  addSampleQuestionButton?.addEventListener("click", function () {
    buildQuestion(true);
  });

  questionList.addEventListener("click", function (event) {
    const target = event.target;
    const card = target.closest("[data-question-card]");

    if (target.matches("[data-add-answer]") && card) {
      addAnswer(card);
    }

    if (target.matches("[data-remove-answer]")) {
      const answerRows = card.querySelectorAll("[data-answer-row]");
      if (answerRows.length > 2) {
        target.closest("[data-answer-row]").remove();
        reindexQuestions();
      }
    }

    if (target.matches("[data-remove-question]")) {
      const cards = questionList.querySelectorAll("[data-question-card]");
      if (cards.length > 1) {
        card.remove();
        reindexQuestions();
      }
    }

    if (target.matches("[data-duplicate-question]") && card) {
      duplicateQuestion(card);
    }
  });

  reindexQuestions();
})();
