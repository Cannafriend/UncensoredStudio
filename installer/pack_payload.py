import zipfile, os

base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
payload_zip = os.path.join(base_dir, 'installer', 'payload.zip')
os.makedirs(os.path.dirname(payload_zip), exist_ok=True)

studio_exe = os.path.join(base_dir, 'Release', 'UncensoredStudio.exe')
if not os.path.exists(studio_exe):
    studio_exe = os.path.join(base_dir, 'UncensoredStudio.exe')

files = [
    (studio_exe, 'UncensoredStudio.exe'),
    (os.path.join(base_dir, 'backend', 'koboldcpp.exe'), os.path.join('backend', 'koboldcpp.exe')),
    (os.path.join(base_dir, 'Logo.png'), 'Logo.png'),
    (os.path.join(base_dir, 'LaunchStudio.bat'), 'LaunchStudio.bat'),
    (os.path.join(base_dir, 'README.md'), 'README.md')
]

with zipfile.ZipFile(payload_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
    for src, dst in files:
        if os.path.exists(src):
            zf.write(src, dst)
            print(f"Packed: {dst}")

    assets_dir = os.path.join(base_dir, 'Assets')
    if os.path.exists(assets_dir):
        for root, dirs, f_list in os.walk(assets_dir):
            for f in f_list:
                full_p = os.path.join(root, f)
                rel_p = os.path.relpath(full_p, base_dir)
                zf.write(full_p, rel_p)
                print(f"Packed asset: {rel_p}")

print(f"Successfully generated {payload_zip} ({os.path.getsize(payload_zip) / (1024**2):.2f} MB)")
