import unittest

from cryptography.exceptions import InvalidTag
from snapshot import decrypt, encrypt


class SnapshotTests(unittest.TestCase):
    def test_round_trip(self):
        data = b"database dump and documents\x00\xff"
        self.assertEqual(data, decrypt(encrypt(data, "test-password"), "test-password"))

    def test_wrong_password(self):
        with self.assertRaises(InvalidTag):
            decrypt(encrypt(b"private", "correct"), "incorrect")

    def test_tampering(self):
        encrypted = bytearray(encrypt(b"private", "correct"))
        encrypted[-1] ^= 1
        with self.assertRaises(InvalidTag):
            decrypt(bytes(encrypted), "correct")


if __name__ == "__main__":
    unittest.main()
