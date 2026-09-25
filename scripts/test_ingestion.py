import urllib.request
import json

def test():
    print("Testing Ingest endpoint...")
    req = urllib.request.Request('http://localhost:5270/api/ingest', data=b'', headers={'Content-Type': 'application/json'}, method='POST')
    with urllib.request.urlopen(req) as resp:
        result = json.loads(resp.read().decode())
        print("Ingestion result:")
        print(f"  Sources checked: {result.get('sourcesChecked')}")
        print(f"  Items discovered: {result.get('itemsDiscovered')}")
        print(f"  New items saved: {result.get('newItemsSaved')}")
        print(f"  Duplicates filtered: {result.get('duplicatesFiltered')}")

    print("\nTesting Feed endpoint (top 5)...")
    req2 = urllib.request.Request('http://localhost:5270/api/feed?limit=5')
    with urllib.request.urlopen(req2) as resp:
        items = json.loads(resp.read().decode())
        print(f"Retrieved {len(items)} items:")
        for idx, item in enumerate(items, 1):
            print(f"  {idx}. [{item.get('sourceName')}] {item.get('title')}")
            print(f"     Canonical URL: {item.get('canonicalUrl')}")

if __name__ == '__main__':
    test()
