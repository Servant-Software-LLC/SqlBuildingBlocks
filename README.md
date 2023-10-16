# SqlBuildingBlocks

## Project Status
**IMPORTANT NOTE:** This project is currently under development and not yet viable for production use. It is a work in progress, and we appreciate your patience and interest.

## Overview
SqlBuildingBlocks is an extensible open-source library, designed to parse SQL into manageable, logical classes tailored to different database technologies. It's built upon Irony's SQLGrammar example and leverages design patterns like Factory and Strategy for customization of SQL parsing, making it an excellent tool for working with SQL across multiple databases.

## Project Objectives
- **Extensibility**: Cater to various database technologies by providing specialized grammars.
- **Usability**: Represent complex SQL grammar in a more manageable, logical, and user-friendly format.
- **Testability**: Offer a strong unit-testing framework to ensure the reliability of the code.

## How It Works
SqlBuildingBlocks breaks down SQL into fundamental 'building blocks', or NonTerminal classes, each of which can handle a specific part of the SQL language. These NonTerminal classes use a factory pattern to create 'logical' classes that represent the elements of the SQL language.

## Future Developments
Our roadmap for SqlBuildingBlocks includes developing custom grammars for popular database technologies such as SQL Server, MySQL and PostgreSQL.  Furthermore, we are working on a general all-purpose query engine which is still in its infancy. Stay tuned for these exciting updates!

## Contributing
We're open to contributions from the community. More details about how to contribute will be shared soon. In the meantime, feel free to explore the repository, and we appreciate your patience.

## License
This project is licensed under the terms of the MIT license. For more information, please see the [LICENSE](LICENSE) file.

## Installation 
Install builds via Nuget.

| Package Name                   | Release (NuGet) |
|--------------------------------|-----------------|
| `SqlBuildingBlocks.Core`       | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Core.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Core/)
| `SqlBuildingBlocks.Grammars.AnsiSQL`       | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.AnsiSQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.AnsiSQL/)
| `SqlBuildingBlocks.Grammars.MySQL`       | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.MySQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.MySQL/)
| `SqlBuildingBlocks.Grammars.PostgreSQL`       | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.PostgreSQL.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.PostgreSQL/)
| `SqlBuildingBlocks.Grammars.SQLServer`       | [![NuGet](https://img.shields.io/nuget/v/SqlBuildingBlocks.Grammars.SQLServer.svg)](https://www.nuget.org/packages/SqlBuildingBlocks.Grammars.SQLServer/)


## Contact
For any inquiries or issues related to SqlBuildingBlocks, please open an issue on GitHub, and we'll do our best to respond promptly.

We're excited to embark on this journey with the community and look forward to seeing SqlBuildingBlocks grow! Stay tuned for more updates as the project progresses.
