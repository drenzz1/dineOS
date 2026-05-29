module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // Task IDs and acronyms (DO-13, ELK, CI/CD, CHANGELOG) naturally start
    // with uppercase — allow sentence-case and start-case in subjects.
    // pascal-case and upper-case (ALL CAPS subject) remain banned.
    'subject-case': [2, 'never', ['pascal-case', 'upper-case']],
  },
};
