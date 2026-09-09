UPDATE `Orders` o
JOIN `Customers` c ON o.`CustomerId` = c.`Id`
SET o.`Status` = 'Closed'
WHERE c.`Region` = 'NA';