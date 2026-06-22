(() => {
  const host = document.getElementById('google_translate_element');
  if (!host) return;

  const current = (document.documentElement.lang || 'vi').toLowerCase();
  const targetMap = {
    en: 'en',
    ja: 'ja',
    ko: 'ko',
    zh: 'zh-CN'
  };
  const target = targetMap[current];

  const clearCookie = () => {
    document.cookie = 'googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
    document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; domain=${location.hostname}`;
  };

  if (!target) {
    clearCookie();
    return;
  }

  const setCookie = () => {
    document.cookie = `googtrans=/vi/${target}; path=/`;
    if (location.hostname.includes('.')) {
      document.cookie = `googtrans=/vi/${target}; path=/; domain=${location.hostname}`;
    }
  };

  const selectTargetLanguage = () => {
    const combo = document.querySelector('.goog-te-combo');
    if (!combo) return false;
    if (combo.value !== target) {
      combo.value = target;
      combo.dispatchEvent(new Event('change'));
    }
    return true;
  };

  window.googleTranslateElementInit = () => {
    setCookie();
    new window.google.translate.TranslateElement({
      pageLanguage: 'vi',
      includedLanguages: 'en,ja,ko,zh-CN',
      autoDisplay: false
    }, 'google_translate_element');

    let attempts = 0;
    const timer = window.setInterval(() => {
      attempts += 1;
      if (selectTargetLanguage() || attempts > 20) {
        window.clearInterval(timer);
      }
    }, 250);
  };

  setCookie();
  const script = document.createElement('script');
  script.src = 'https://translate.google.com/translate_a/element.js?cb=googleTranslateElementInit';
  script.async = true;
  document.head.append(script);
})();
