import re

files = [
    'MES.Tests/Services/BatchServiceTests.cs',
    'MES.Tests/Services/ProductionRecordServiceTests.cs',
    'MES.Tests/Services/ProcessInspectionServiceTests.cs',
    'MES.Tests/Services/SectionOutsourceServiceTests.cs',
    'MES.Tests/Services/FinalInspectionServiceTests.cs',
]

for filepath in files:
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Fix inline pattern: ProductionType = "RoughTube" });
    content = re.sub(
        r'ProductionType = "RoughTube" \}\);',
        'ProductionType = "RoughTube", ManufacturingItem = "\u8ba2\u5355\u6210\u54c1" });',
        content
    )

    # Fix multi-line pattern: ProductionType = "RoughTube", followed by non-ManufacturingItem
    content = re.sub(
        r'(ProductionType = "RoughTube",\n\s+)(?!ManufacturingItem)',
        lambda m: m.group(1) + 'ManufacturingItem = "\u8ba2\u5355\u6210\u54c1",\n            ',
        content
    )

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

    print(f'Fixed: {filepath}')
