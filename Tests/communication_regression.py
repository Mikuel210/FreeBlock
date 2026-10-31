"""Integration regressions: run against an installed, running test daemon.

Uses only reserved .invalid domains. Do not run against a personal installation.
Run: python3 -m unittest discover -s Tests -p '*regression.py' -v
"""
import subprocess
import unittest


def cli(*args):
    result = subprocess.run(['freeblock', *args], input='yes\n' * 8,
                            text=True, capture_output=True, timeout=20)
    if result.returncode:
        raise AssertionError(result.stdout + result.stderr)
    return result.stdout


class CommunicationRegression(unittest.TestCase):
    def test_config_roundtrip(self):
        key = 'status.showAllLists'
        old = cli('config', 'get', key).strip()
        try:
            cli('config', 'set', key, 'true')
            self.assertEqual('True', cli('config', 'get', key).strip())
        finally:
            cli('config', 'set', key, old.lower())

    def test_block_roundtrip(self):
        domain = 'freeblock-regression.invalid'
        try:
            cli('block', domain)
            self.assertIn(domain, cli('status'))
            with open('/etc/hosts') as hosts:
                self.assertIn('0.0.0.0 ' + domain, hosts.read())
        finally:
            cli('unblock', domain)
        self.assertNotIn(domain, cli('status'))


if __name__ == '__main__':
    unittest.main()
