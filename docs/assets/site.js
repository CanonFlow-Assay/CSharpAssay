(() => {
  'use strict';

  const header = document.querySelector('[data-header]');
  const navToggle = document.querySelector('[data-nav-toggle]');
  const navMenu = document.querySelector('[data-nav-menu]');
  const toast = document.querySelector('[data-copy-toast]');
  let toastTimer;

  const updateHeader = () => {
    header?.classList.toggle('scrolled', window.scrollY > 12);
  };

  const closeMenu = () => {
    navToggle?.setAttribute('aria-expanded', 'false');
    navMenu?.classList.remove('open');
  };

  navToggle?.addEventListener('click', () => {
    const isOpen = navToggle.getAttribute('aria-expanded') === 'true';
    navToggle.setAttribute('aria-expanded', String(!isOpen));
    navMenu?.classList.toggle('open', !isOpen);
  });

  navMenu?.querySelectorAll('a').forEach((link) => link.addEventListener('click', closeMenu));

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      closeMenu();
      navToggle?.focus();
    }
  });

  document.addEventListener('click', (event) => {
    if (!navMenu?.classList.contains('open')) return;
    if (!navMenu.contains(event.target) && !navToggle?.contains(event.target)) closeMenu();
  });

  document.querySelectorAll('[data-copy-target]').forEach((button) => {
    button.addEventListener('click', async () => {
      const target = document.getElementById(button.dataset.copyTarget);
      if (!target) return;
      const value = target.innerText.trim();

      try {
        await navigator.clipboard.writeText(value);
      } catch {
        const area = document.createElement('textarea');
        area.value = value;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        document.execCommand('copy');
        area.remove();
      }

      const label = button.querySelector('span');
      const original = label?.textContent ?? 'Copy';
      if (label) label.textContent = 'Copied';
      button.classList.add('copied');
      toast?.classList.add('visible');
      window.clearTimeout(toastTimer);
      toastTimer = window.setTimeout(() => toast?.classList.remove('visible'), 1800);
      window.setTimeout(() => {
        if (label) label.textContent = original;
        button.classList.remove('copied');
      }, 1800);
    });
  });

  const navLinks = [...document.querySelectorAll('.nav-menu a[href^="#"]')];
  const sections = navLinks
    .map((link) => document.querySelector(link.getAttribute('href')))
    .filter(Boolean);

  if ('IntersectionObserver' in window && sections.length) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        navLinks.forEach((link) => {
          const active = link.getAttribute('href') === `#${entry.target.id}`;
          link.classList.toggle('active', active);
          if (active) link.setAttribute('aria-current', 'location');
          else link.removeAttribute('aria-current');
        });
      });
    }, { rootMargin: '-22% 0px -68% 0px', threshold: 0 });

    sections.forEach((section) => observer.observe(section));
  }

  window.addEventListener('scroll', updateHeader, { passive: true });
  updateHeader();
})();
