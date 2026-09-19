I want to implement a module for the fleet card application, which will be used for a client. This is an MVP for now, so there are some simplifications.

The module consists of a web front-end, where the client can:
 1. login with an existing username and password (no self-onboarding for MVP). Auth provider is mocked. Several users seeded into the database
 1. see the client details (Polish jurisdiction).
 1. change the front-end language (Polish and English are supported for MVP)
 1. manage the list of cars in the client fleet (the main property is the number plate) - add/remove/edit/search
 1. manage list of fleet cards (PAN is masked and never stored in the system DB):
     - issue a new virtual card and store it in the DB: card provider is mocked and the system gets just the masked PAN and a unique card token. PIN management is out of MVP scope. BIN is handled by the card provider. PAN is 16 digits long with Luhn.
	 - return the card to the provider (remove from the list)
	 - mark card as blocked or stolen
	 - assign a card to a car (one car can have multiple cards)
 1. See all card transactions (see below) and total amount with discount and without for a selected period.
 1. Assign the whole client to one of fleet card programs or remove the assignement (more about them later)
     - The system shows how much the client would pay if they selected the particular program with the current set of transations.
	 - This is the business logic, which help the client to decide which program to choose. Programs are created in a way, that if you have various cars, which require petrol and diesel, then depending on what is used more, you may get different results. Sometimes it's even better to completely remove the assignement of the fleet card program and pay the price on the station.	 
 
 Card transactions are mocked: once a new card is issued the new random set of 1-5 fuel transactions are generated for last month. The transaction has:
     - date/time
	 - point of service (Petrol Station - a random name like "angry storage", which GitHub uses to name repositories
	 - fuel type from the limited list (95, 98, Diesel, 95+, 98+, Diesel+)
	 - fuel quantity (3 digits after comma)
	 - fuel price without a discount (2 digits after comma)
	 - discount on fuel price (2 digits after comma) - can be negative
	 - fuel total amount without discount (2 digits after comma)
	 - fuel total amount paid with discount (2 digits after comma): [fuel quantity] * ([fuel price] - [discount on fuel price])
  
  "Fleet card programs" are mocked for now - just a json with the setup. They are created by an Operations Team in another module and contain the following details:
     - name
	 - period of activity (when not active, client pays the price without any discounts)
     - fuel type (95, 98, Diesel, 95+, 98+, Diesel+)
	 - percent of discount (or premium if negative) from the price on the petrol station 	 

Technology stack is pre-defined:
 - Backend: .NET 10 ASP.NET with REST API (Azure Function or App Service in Free tier)
 - Frontend: Angular SPA + typescript hosted by the backend
 - Database: Azure SQL if relational option is preferable (free tier) or Azure Table Storage (if no-sql key-value pair store is enough) or Cosmos DB (free tier)
 - Hosting environment: Azure (as many free-tier services 
 - Deployment: Github actions for infra (terraform) and application 

Now if a business have some fleet cars and they want to tank them they go to a specific petrol company (BP, Orlen) and sign a contract with them for some amount of fuel. The main benefit of it is that why buy fuel using netto price, instead of brutto. Our idea is to be an aggregator, so one fleet car can be tanked on many pertrol station within the country and abroad. 
For this MVP we concentrate on end users, namely the company representatives who want to use our cards to fuel their vehicles (not sesessary cars - it can be motorcycles or 8-whellers). They will use this module when they want to see what cards they have and how much money was spent for, say last month. They may want to block an existing card or issue a new one (it's a virtual card in our mobile application). Also, they may decide to switch to another fuel card program starting from moment X in future. Only one or zero programs can be active at the same time. The timeline of which program was active when is kept and immutable. There are cases that transactions can be refunded, cancelled, or recalculated, so we must know when each program was active.
I also realized we need to support VAT in the transactions history. 
From the installation perspective - each client will have their own Azure infrastructure. The only shared thing is the Fleet Card Programms, which are stored centrally and copied to the client installation if they choose it.




